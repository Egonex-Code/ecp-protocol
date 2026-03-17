// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Registry;

/// <summary>
/// Provides access to template text by language.
/// </summary>
public interface ITemplateProvider
{
    /// <summary>
    /// Resolves a template by id, version, and language.
    /// </summary>
    bool TryResolve(byte templateSetId, byte templateVersion, string languageCode, out string templateText);

    /// <summary>
    /// Returns true when the template set is available for the given id and version.
    /// </summary>
    bool HasTemplate(byte templateSetId, byte templateVersion);
}
