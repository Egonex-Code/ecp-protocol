// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Models;

/// <summary>
/// Priority level for an emergency message.
/// </summary>
public enum EcpPriority : byte
{
    /// <summary>Low priority.</summary>
    Low = 0,
    /// <summary>Medium priority.</summary>
    Medium = 1,
    /// <summary>High priority.</summary>
    High = 2,
    /// <summary>Critical priority.</summary>
    Critical = 3
}
