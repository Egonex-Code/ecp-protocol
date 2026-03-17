// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ECP.Core.Registry;

namespace ECP.Registry.Dictionary;

/// <summary>
/// Two-level emergency dictionary (global + tenant) for payload compression.
/// </summary>
public sealed class EmergencyDictionary : IDictionaryProvider
{
    private const byte GlobalLevel = 0;
    private const byte TenantLevel = 1;
    private const byte LevelMask = 0b1000_0000;
    private const byte VersionMask = 0b0111_1111;

    private readonly Dictionary<string, byte> _globalLookup;
    private readonly Dictionary<byte, string> _globalReverse;
    private readonly Dictionary<string, byte> _tenantLookup;
    private readonly Dictionary<byte, string> _tenantReverse;

    /// <summary>Dictionary identifier.</summary>
    public byte DictionaryId { get; }

    /// <summary>Dictionary version.</summary>
    public byte DictionaryVersion { get; }

    /// <summary>Dictionary hash (16-bit) for synchronization checks.</summary>
    public ushort DictionaryHash { get; }

    /// <summary>
    /// Creates a dictionary with explicit global and tenant terms.
    /// </summary>
    public EmergencyDictionary(
        byte dictionaryId,
        byte dictionaryVersion,
        IReadOnlyList<string> globalTerms,
        IReadOnlyList<string> tenantTerms)
    {
        if (dictionaryVersion > VersionMask)
        {
            throw new ArgumentOutOfRangeException(nameof(dictionaryVersion), "Dictionary version must fit in 7 bits.");
        }

        DictionaryId = dictionaryId;
        DictionaryVersion = dictionaryVersion;

        _globalLookup = BuildLookup(globalTerms, out _globalReverse);
        _tenantLookup = BuildLookup(tenantTerms, out _tenantReverse);

        DictionaryHash = ComputeHash(globalTerms, tenantTerms);
    }

    /// <summary>
    /// Creates a default dictionary with common emergency terms.
    /// </summary>
    public static EmergencyDictionary CreateDefault(byte dictionaryId = 1, byte dictionaryVersion = 1)
    {
        var globalTerms = new[]
        {
            "immediate",
            "evacuation",
            "fire",
            "alarm",
            "earthquake",
            "flood",
            "zone",
            "floor",
            "stairway",
            "building",
            "at",
            "now"
        };

        var tenantTerms = new[]
        {
            "Gate",
            "B2",
            "Terminal",
            "3"
        };

        return new EmergencyDictionary(dictionaryId, dictionaryVersion, globalTerms, tenantTerms);
    }

    /// <summary>
    /// Attempts to compress UTF-8 text using the dictionary.
    /// </summary>
    public bool TryCompress(ReadOnlySpan<byte> input, Span<byte> output, out int bytesWritten)
    {
        bytesWritten = 0;
        if (input.IsEmpty)
        {
            return false;
        }

        string text;
        try
        {
            text = Encoding.UTF8.GetString(input);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        var tokens = Tokenize(text);
        if (tokens.Count == 0 || tokens.Count > byte.MaxValue)
        {
            return false;
        }

        var tokenCount = tokens.Count;
        Span<byte> globalIds = stackalloc byte[tokenCount];
        Span<byte> tenantIds = stackalloc byte[tokenCount];
        if (!TryBuildTokenIds(
            tokens,
            globalIds,
            tenantIds,
            out var globalOnlyCount,
            out var tenantOnlyCount,
            out var allGlobal,
            out var allTenant))
        {
            return false;
        }

        if (allTenant)
        {
            return TryEncodeSingleLevel(tenantIds, TenantLevel, output, out bytesWritten);
        }

        if (allGlobal)
        {
            return TryEncodeSingleLevel(globalIds, GlobalLevel, output, out bytesWritten);
        }

        var defaultLevel = globalOnlyCount >= tenantOnlyCount ? GlobalLevel : TenantLevel;
        return TryEncodeMixed(globalIds, tenantIds, defaultLevel, output, out bytesWritten);
    }

    /// <summary>
    /// Attempts to decompress UTF-8 text using the dictionary.
    /// </summary>
    public bool TryDecompress(ReadOnlySpan<byte> input, Span<byte> output, out int bytesWritten)
    {
        bytesWritten = 0;
        if (input.Length < 3)
        {
            return false;
        }

        if (input[0] != DictionaryId)
        {
            return false;
        }

        var versionAndLevel = input[1];
        var level = (byte)((versionAndLevel & LevelMask) >> 7);
        var version = (byte)(versionAndLevel & VersionMask);
        if (version != DictionaryVersion)
        {
            return false;
        }

        var tokenCount = input[2];
        if (!TryGetReverseDictionaries(level, out var defaultReverse, out var alternateReverse))
        {
            return false;
        }

        if (!TryDecodeTokens(input.Slice(3), tokenCount, defaultReverse, alternateReverse, out var terms))
        {
            return false;
        }

        var text = string.Join(' ', terms);
        var bytes = Encoding.UTF8.GetBytes(text);
        if (output.Length < bytes.Length)
        {
            return false;
        }

        bytes.CopyTo(output);
        bytesWritten = bytes.Length;
        return true;
    }

    private static Dictionary<string, byte> BuildLookup(IReadOnlyList<string> terms, out Dictionary<byte, string> reverse)
    {
        var lookup = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        reverse = new Dictionary<byte, string>();

        byte index = 1;
        foreach (var term in terms)
        {
            if (index == 0)
            {
                throw new InvalidOperationException("Dictionary exceeds maximum size.");
            }

            lookup[term] = index;
            reverse[index] = term;
            index++;
        }

        return lookup;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        foreach (var raw in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = NormalizeToken(raw);
            if (token.Length > 0)
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    private static string NormalizeToken(string token)
    {
        return token.Trim().Trim('.', ',', ';', ':', '!', '?');
    }

    private bool TryBuildTokenIds(
        List<string> tokens,
        Span<byte> globalIds,
        Span<byte> tenantIds,
        out int globalOnlyCount,
        out int tenantOnlyCount,
        out bool allGlobal,
        out bool allTenant)
    {
        globalOnlyCount = 0;
        tenantOnlyCount = 0;
        allGlobal = true;
        allTenant = true;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var hasGlobal = _globalLookup.TryGetValue(token, out var globalId);
            var hasTenant = _tenantLookup.TryGetValue(token, out var tenantId);

            if (!hasGlobal && !hasTenant)
            {
                return false;
            }

            if (hasGlobal)
            {
                globalIds[i] = globalId;
            }
            else
            {
                allGlobal = false;
            }

            if (hasTenant)
            {
                tenantIds[i] = tenantId;
            }
            else
            {
                allTenant = false;
            }

            if (hasGlobal != hasTenant)
            {
                if (hasGlobal)
                {
                    globalOnlyCount++;
                }
                else
                {
                    tenantOnlyCount++;
                }
            }
        }

        return true;
    }

    private bool TryGetReverseDictionaries(
        byte level,
        out Dictionary<byte, string> defaultReverse,
        out Dictionary<byte, string> alternateReverse)
    {
        defaultReverse = level switch
        {
            GlobalLevel => _globalReverse,
            TenantLevel => _tenantReverse,
            _ => null!
        };
        alternateReverse = level switch
        {
            GlobalLevel => _tenantReverse,
            TenantLevel => _globalReverse,
            _ => null!
        };

        return defaultReverse is not null && alternateReverse is not null;
    }

    private static bool TryDecodeTokens(
        ReadOnlySpan<byte> input,
        int tokenCount,
        Dictionary<byte, string> defaultReverse,
        Dictionary<byte, string> alternateReverse,
        out string[] terms)
    {
        terms = new string[tokenCount];
        var inputIndex = 0;

        for (var i = 0; i < tokenCount; i++)
        {
            if (inputIndex >= input.Length)
            {
                return false;
            }

            var token = input[inputIndex++];
            var reverse = defaultReverse;
            if (token == 0)
            {
                if (inputIndex >= input.Length)
                {
                    return false;
                }

                token = input[inputIndex++];
                reverse = alternateReverse;
            }

            if (!reverse.TryGetValue(token, out var term))
            {
                return false;
            }

            terms[i] = term;
        }

        return inputIndex == input.Length;
    }

    private bool TryEncodeSingleLevel(ReadOnlySpan<byte> tokenIds, byte level, Span<byte> output, out int bytesWritten)
    {
        bytesWritten = 0;
        var required = 3 + tokenIds.Length;
        if (output.Length < required)
        {
            return false;
        }

        output[0] = DictionaryId;
        output[1] = (byte)(DictionaryVersion | (level << 7));
        output[2] = (byte)tokenIds.Length;

        for (var i = 0; i < tokenIds.Length; i++)
        {
            var tokenId = tokenIds[i];
            if (tokenId == 0)
            {
                return false;
            }

            output[3 + i] = tokenId;
        }

        bytesWritten = required;
        return true;
    }

    private bool TryEncodeMixed(
        ReadOnlySpan<byte> globalIds,
        ReadOnlySpan<byte> tenantIds,
        byte defaultLevel,
        Span<byte> output,
        out int bytesWritten)
    {
        bytesWritten = 0;
        var tokenCount = globalIds.Length;
        var escapeCount = 0;

        for (var i = 0; i < tokenCount; i++)
        {
            var hasDefault = defaultLevel == GlobalLevel
                ? globalIds[i] != 0
                : tenantIds[i] != 0;

            if (!hasDefault)
            {
                escapeCount++;
            }
        }

        var required = 3 + tokenCount + escapeCount;
        if (output.Length < required)
        {
            return false;
        }

        output[0] = DictionaryId;
        output[1] = (byte)(DictionaryVersion | (defaultLevel << 7));
        output[2] = (byte)tokenCount;

        var offset = 3;
        for (var i = 0; i < tokenCount; i++)
        {
            if (defaultLevel == GlobalLevel)
            {
                var globalId = globalIds[i];
                if (globalId != 0)
                {
                    output[offset++] = globalId;
                    continue;
                }

                var tenantId = tenantIds[i];
                if (tenantId == 0)
                {
                    return false;
                }

                output[offset++] = 0;
                output[offset++] = tenantId;
                continue;
            }

            var tenantToken = tenantIds[i];
            if (tenantToken != 0)
            {
                output[offset++] = tenantToken;
                continue;
            }

            var globalToken = globalIds[i];
            if (globalToken == 0)
            {
                return false;
            }

            output[offset++] = 0;
            output[offset++] = globalToken;
        }

        bytesWritten = offset;
        return true;
    }

    private static ushort ComputeHash(IReadOnlyList<string> globalTerms, IReadOnlyList<string> tenantTerms)
    {
        var combined = string.Join('|', globalTerms) + "||" + string.Join('|', tenantTerms);
        var data = Encoding.UTF8.GetBytes(combined);
        var hash = SHA256.HashData(data);
        return (ushort)((hash[0] << 8) | hash[1]);
    }
}
