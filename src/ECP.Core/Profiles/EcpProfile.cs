// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Profiles;

/// <summary>
/// Predefined ECP configuration profiles.
/// </summary>
public enum EcpProfile : byte
{
    /// <summary>Core only (UET, Envelope, Security).</summary>
    Minimal = 0,
    /// <summary>Core + Strategy + Confirmation + GeoQuorum.</summary>
    Standard = 1,
    /// <summary>Standard + Registry + Cascade.</summary>
    Enterprise = 2,
    /// <summary>Enterprise + multilingual templates (future: heartbeat).</summary>
    Airport = 3,
    /// <summary>Enterprise (future: proof of delivery).</summary>
    Hospital = 4,
    /// <summary>Minimal with high-frequency tuning.</summary>
    Industrial = 5
}
