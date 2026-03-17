// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Registry;

/// <summary>
/// Typed parameter used by multilingual templates.
/// </summary>
public readonly record struct TemplateParameter(
    string Name,
    TemplateParamType Type,
    object Value);

/// <summary>
/// Parameter types supported by templates.
/// </summary>
public enum TemplateParamType : byte
{
    /// <summary>Numeric value (int, double).</summary>
    Number = 0,
    /// <summary>Geographic zone (16-bit geohash).</summary>
    GeoZone = 1,
    /// <summary>Building floor (coded reference).</summary>
    Floor = 2,
    /// <summary>Stairway (coded reference).</summary>
    Stairway = 3,
    /// <summary>Compression dictionary reference.</summary>
    DictionaryRef = 4,
    /// <summary>Airport gate (coded reference).</summary>
    Gate = 5,
    /// <summary>Sector (coded reference).</summary>
    Sector = 6
}
