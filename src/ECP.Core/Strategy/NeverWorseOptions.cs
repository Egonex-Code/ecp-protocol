// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Strategy;

/// <summary>
/// Configurable thresholds used by <see cref="NeverWorseSelector"/>.
/// </summary>
public sealed class NeverWorseOptions
{
    /// <summary>
    /// Recipient count threshold under which direct delivery is preferred.
    /// </summary>
    public int DirectThreshold { get; set; } = 4;

    /// <summary>
    /// Recipient count threshold under which mini-cascade is evaluated.
    /// </summary>
    public int MiniCascadeThreshold { get; set; } = 12;

    /// <summary>
    /// Fan-out factor used for mini-cascade transmission cost estimation.
    /// </summary>
    public double MiniCascadeFanOutFactor { get; set; } = 2.5;

    /// <summary>
    /// Multiplicative factor applied when dictionary compression is available.
    /// </summary>
    public double DictionarySavingsFactor { get; set; } = 0.78;

    /// <summary>
    /// Multiplicative factor applied when template compression is available.
    /// </summary>
    public double TemplateSavingsFactor { get; set; } = 0.55;
}
