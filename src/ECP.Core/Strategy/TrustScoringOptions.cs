// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Strategy;

/// <summary>
/// Configurable thresholds and fan-out tiers for trust scoring.
/// </summary>
public sealed class TrustScoringOptions
{
    /// <summary>
    /// Minimum allowed node trust score.
    /// </summary>
    public int MinScore { get; set; }

    /// <summary>
    /// Maximum allowed node trust score.
    /// </summary>
    public int MaxScore { get; set; } = 100;

    /// <summary>
    /// Fan-out returned when score is below mid threshold.
    /// </summary>
    public int LowFanOut { get; set; } = 3;

    /// <summary>
    /// Fan-out returned when score is at or above mid threshold and below high threshold.
    /// </summary>
    public int MidFanOut { get; set; } = 6;

    /// <summary>
    /// Fan-out returned when score is at or above high threshold.
    /// </summary>
    public int HighFanOut { get; set; } = 9;

    /// <summary>
    /// Score threshold for medium tier.
    /// </summary>
    public int MidScoreThreshold { get; set; } = 45;

    /// <summary>
    /// Score threshold for high tier.
    /// </summary>
    public int HighScoreThreshold { get; set; } = 75;

    /// <summary>
    /// Default score assigned to unknown nodes.
    /// </summary>
    public int DefaultScore { get; set; } = 55;
}
