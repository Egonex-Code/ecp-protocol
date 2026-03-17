// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Strategy;

/// <summary>
/// Delivery mode selected by the strategy selector.
/// </summary>
public enum DeliveryMode
{
    /// <summary>Direct send to each recipient.</summary>
    Direct,
    /// <summary>Mini cascade with a small number of hops.</summary>
    MiniCascade,
    /// <summary>Full cascade optimized for large audiences.</summary>
    FullCascade,
    /// <summary>UET-only delivery (8 bytes).</summary>
    UetOnly
}
