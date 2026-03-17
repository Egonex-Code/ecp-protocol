// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Models;

/// <summary>
/// Capability bitmap used during negotiation.
/// </summary>
[Flags]
public enum EcpCapabilities : ushort
{
    /// <summary>No optional capabilities.</summary>
    None = 0,
    /// <summary>Supports dictionary compression.</summary>
    SupportsDictionary = 1 << 0,
    /// <summary>Supports multilingual templates.</summary>
    SupportsTemplates = 1 << 1,
    /// <summary>Supports cascade broadcast.</summary>
    SupportsCascade = 1 << 2,
    /// <summary>Supports encryption (AES-GCM).</summary>
    SupportsEncryption = 1 << 3,
    /// <summary>Supports compression (LZ4).</summary>
    SupportsCompression = 1 << 4
}
