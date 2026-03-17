// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ECP.Core.Registry;

namespace ECP.Registry.Templates;

/// <summary>
/// In-memory template registry for multilingual templates.
/// </summary>
public sealed class TemplateRegistry : ITemplateProvider
{
    private readonly Dictionary<string, string> _templates;
    private ushort _cachedHash;
    private bool _hashDirty = true;

    /// <summary>Template set identifier.</summary>
    public byte TemplateSetId { get; }

    /// <summary>Template set version.</summary>
    public byte TemplateVersion { get; }

    /// <summary>Template set hash (16-bit) for synchronization checks.</summary>
    public ushort TemplateHash
    {
        get
        {
            if (_hashDirty)
            {
                _cachedHash = ComputeHash();
                _hashDirty = false;
            }

            return _cachedHash;
        }
    }

    /// <summary>
    /// Creates a template registry for a single template set.
    /// </summary>
    public TemplateRegistry(byte templateSetId, byte templateVersion)
    {
        TemplateSetId = templateSetId;
        TemplateVersion = templateVersion;
        _templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Adds or replaces a template for a language code.
    /// </summary>
    public void AddTemplate(string languageCode, string template)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            throw new ArgumentException("Language code is required.", nameof(languageCode));
        }

        if (string.IsNullOrEmpty(template))
        {
            throw new ArgumentException("Template text is required.", nameof(template));
        }

        _templates[languageCode] = template;
        _hashDirty = true;
    }

    /// <summary>
    /// Resolves a template by id, version, and language.
    /// </summary>
    public bool TryResolve(byte templateSetId, byte templateVersion, string languageCode, out string templateText)
    {
        templateText = string.Empty;

        if (!HasTemplate(templateSetId, templateVersion))
        {
            return false;
        }

        if (!_templates.TryGetValue(languageCode, out var template) || template is null)
        {
            return false;
        }

        templateText = template;
        return true;
    }

    /// <summary>
    /// Returns true when the template set matches id and version.
    /// </summary>
    public bool HasTemplate(byte templateSetId, byte templateVersion)
    {
        return templateSetId == TemplateSetId && templateVersion == TemplateVersion;
    }

    /// <summary>
    /// Validates a template hash against the current registry content.
    /// </summary>
    public bool ValidateHash(ushort expectedHash)
    {
        return TemplateHash == expectedHash;
    }

    private ushort ComputeHash()
    {
        var entries = new List<KeyValuePair<string, string>>(_templates);
        entries.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Key, b.Key));

        var builder = new StringBuilder();
        foreach (var entry in entries)
        {
            builder.Append(entry.Key);
            builder.Append('=');
            builder.Append(entry.Value);
            builder.Append('|');
        }

        var data = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = SHA256.HashData(data);
        return (ushort)((hash[0] << 8) | hash[1]);
    }
}
