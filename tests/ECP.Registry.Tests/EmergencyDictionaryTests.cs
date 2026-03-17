// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Text;
using ECP.Registry.Dictionary;
using Xunit;

namespace ECP.Registry.Tests;

public class EmergencyDictionaryTests
{
    [Fact]
    public void CompressDecompressRoundtripGlobal()
    {
        var dictionary = EmergencyDictionary.CreateDefault();
        var inputText = "immediate evacuation";
        var input = Encoding.UTF8.GetBytes(inputText);

        Span<byte> compressed = stackalloc byte[64];
        var ok = dictionary.TryCompress(input, compressed, out var bytesWritten);

        Assert.True(ok);

        Span<byte> decompressed = stackalloc byte[128];
        var decodedOk = dictionary.TryDecompress(compressed.Slice(0, bytesWritten), decompressed, out var decodedBytes);

        Assert.True(decodedOk);

        var outputText = Encoding.UTF8.GetString(decompressed.Slice(0, decodedBytes));
        Assert.Equal(inputText, outputText, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GlobalDictionaryCompressesBelowHalf()
    {
        var dictionary = EmergencyDictionary.CreateDefault();
        var input = Encoding.UTF8.GetBytes("immediate evacuation");

        Span<byte> compressed = stackalloc byte[64];
        var ok = dictionary.TryCompress(input, compressed, out var bytesWritten);

        Assert.True(ok);
        Assert.True(bytesWritten * 2 < input.Length);
    }

    [Fact]
    public void TenantDictionaryCompressesBelow40Percent()
    {
        var dictionary = EmergencyDictionary.CreateDefault();
        var input = Encoding.UTF8.GetBytes("Gate B2 Terminal 3");

        Span<byte> compressed = stackalloc byte[64];
        var ok = dictionary.TryCompress(input, compressed, out var bytesWritten);

        Assert.True(ok);
        Assert.True(bytesWritten * 100 < input.Length * 40);
    }

    [Fact]
    public void MixedDictionaryCompressDecompressRoundtrip()
    {
        var dictionary = EmergencyDictionary.CreateDefault();
        var inputText = "fire at Gate B2 now";
        var input = Encoding.UTF8.GetBytes(inputText);

        Span<byte> compressed = stackalloc byte[64];
        var ok = dictionary.TryCompress(input, compressed, out var bytesWritten);

        Assert.True(ok);

        Span<byte> decompressed = stackalloc byte[128];
        var decodedOk = dictionary.TryDecompress(compressed.Slice(0, bytesWritten), decompressed, out var decodedBytes);

        Assert.True(decodedOk);

        var outputText = Encoding.UTF8.GetString(decompressed.Slice(0, decodedBytes));
        Assert.Equal(inputText, outputText, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompressionFallbacksWhenTermIsUnknown()
    {
        var dictionary = EmergencyDictionary.CreateDefault();
        var input = Encoding.UTF8.GetBytes("unknown token");

        Span<byte> compressed = stackalloc byte[64];
        var ok = dictionary.TryCompress(input, compressed, out _);

        Assert.False(ok);
    }

    [Fact]
    public void HeaderContainsDictionaryIdAndVersion()
    {
        var dictionary = EmergencyDictionary.CreateDefault(dictionaryId: 7, dictionaryVersion: 9);
        var input = Encoding.UTF8.GetBytes("immediate evacuation");

        Span<byte> compressed = stackalloc byte[64];
        var ok = dictionary.TryCompress(input, compressed, out _);

        Assert.True(ok);
        Assert.Equal(7, compressed[0]);
        Assert.Equal(9, compressed[1] & 0x7F);
    }

    [Fact]
    public void DictionaryHashIsNonZero()
    {
        var dictionary = EmergencyDictionary.CreateDefault();
        Assert.NotEqual(0, dictionary.DictionaryHash);
    }

    [Fact]
    public void DictionaryCompressesFullMessageOver70Percent()
    {
        var dictionary = EmergencyDictionary.CreateDefault();
        var inputText = "immediate evacuation immediate evacuation immediate evacuation immediate evacuation immediate evacuation";
        var input = Encoding.UTF8.GetBytes(inputText);

        Span<byte> compressed = stackalloc byte[128];
        var ok = dictionary.TryCompress(input, compressed, out var bytesWritten);

        Assert.True(ok);
        Assert.True(bytesWritten * 100 <= input.Length * 30);
    }
}
