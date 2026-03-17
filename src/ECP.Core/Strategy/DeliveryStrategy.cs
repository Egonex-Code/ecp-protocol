// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Strategy;

/// <summary>
/// Selected delivery strategy with cost estimation details.
/// </summary>
/// <param name="Mode">Chosen delivery mode.</param>
/// <param name="EstimatedTotalBytes">
/// Estimated total bytes for the chosen strategy. For <see cref="DeliveryMode.UetOnly"/>, this is
/// the single UET size (8 bytes) since the token is broadcast rather than per-recipient.
/// </param>
/// <param name="HopCount">Estimated hop count for cascade delivery.</param>
/// <param name="Reasoning">Diagnostic reasoning for the selection.</param>
public readonly record struct DeliveryStrategy(
    DeliveryMode Mode,
    int EstimatedTotalBytes,
    int HopCount,
    string Reasoning);
