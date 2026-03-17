// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Models;

/// <summary>
/// Emergency type encoded in the UET header.
/// </summary>
public enum EmergencyType : byte
{
    /// <summary>Fire.</summary>
    Fire = 0,
    /// <summary>Evacuation.</summary>
    Evacuation = 1,
    /// <summary>Earthquake.</summary>
    Earthquake = 2,
    /// <summary>Flood.</summary>
    Flood = 3,
    /// <summary>Medical emergency.</summary>
    Medical = 4,
    /// <summary>Security incident.</summary>
    Security = 5,
    /// <summary>Chemical incident.</summary>
    Chemical = 6,
    /// <summary>Lockdown.</summary>
    Lockdown = 7,
    /// <summary>All clear / end of emergency.</summary>
    AllClear = 8,
    /// <summary>Test message.</summary>
    Test = 9,
    /// <summary>Custom emergency type 1.</summary>
    Custom1 = 10,
    /// <summary>Custom emergency type 2.</summary>
    Custom2 = 11,
    /// <summary>Custom emergency type 3.</summary>
    Custom3 = 12,
    /// <summary>Custom emergency type 4.</summary>
    Custom4 = 13,
    /// <summary>Custom emergency type 5.</summary>
    Custom5 = 14,
    /// <summary>Reserved for future use.</summary>
    Reserved = 15
}
