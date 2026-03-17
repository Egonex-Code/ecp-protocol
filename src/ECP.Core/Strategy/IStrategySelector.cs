// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Strategy;

/// <summary>
/// Selects the best delivery strategy based on recipient count and message size.
/// </summary>
public interface IStrategySelector
{
    /// <summary>
    /// Selects the delivery strategy that minimizes byte cost.
    /// </summary>
    DeliveryStrategy SelectStrategy(int recipientCount, int messageSize, bool hasTemplate = false, bool hasDictionary = false);
}
