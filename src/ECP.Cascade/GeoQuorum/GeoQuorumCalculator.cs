// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Cascade.GeoQuorum;

/// <summary>
/// Calculates geographic quorum coverage per zone.
/// </summary>
public sealed class GeoQuorumCalculator
{
    /// <summary>
    /// Calculates coverage percentage per zone.
    /// </summary>
    public static IReadOnlyList<GeoQuorumResult> Calculate(IReadOnlyList<ZoneConfirmationStats> zones)
    {
        ArgumentNullException.ThrowIfNull(zones);

        var results = new List<GeoQuorumResult>(zones.Count);
        foreach (var zone in zones)
        {
            var coverage = ComputeCoverage(zone.ConfirmedCount, zone.ExpectedCount);
            results.Add(new GeoQuorumResult(zone.ZoneHash, coverage, zone.ConfirmedCount, zone.ExpectedCount));
        }

        results.Sort(static (left, right) => left.ZoneHash.CompareTo(right.ZoneHash));
        return results;
    }

    private static double ComputeCoverage(int confirmedCount, int expectedCount)
    {
        if (expectedCount <= 0)
        {
            return 0d;
        }

        var clamped = confirmedCount > expectedCount ? expectedCount : confirmedCount;
        return (clamped / (double)expectedCount) * 100d;
    }
}
