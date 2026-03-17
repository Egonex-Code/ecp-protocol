// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Privacy;
using ECP.Core.Tenancy;

namespace ECP.Core.Tests;

public class ZoneHashProviderTests
{
    [Fact]
    public void ZoneHashChangesAcrossEpochs()
    {
        var options = new EcpPrivacyOptions
        {
            EpochDuration = TimeSpan.FromMinutes(1),
            AnonymizeZoneHash = true,
            ZoneHashSalt = new byte[] { 0x01, 0x02 }
        };
        var provider = new DefaultPrivacyOptionsProvider(options);
        var tenantContext = new DefaultTenantContext("tenant-a");
        var hashProvider = new ZoneHashProvider(provider, tenantContext);

        var now = new DateTimeOffset(2026, 2, 7, 10, 0, 0, TimeSpan.Zero);
        var hash1 = hashProvider.ComputeZoneHash("Zone-A", "tenant-a", now);
        var hash2 = hashProvider.ComputeZoneHash("Zone-A", "tenant-a", now.AddMinutes(2));

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ZoneHashDiffersAcrossTenants()
    {
        var options = new EcpPrivacyOptions
        {
            EpochDuration = TimeSpan.FromMinutes(15),
            AnonymizeZoneHash = true,
            ZoneHashSalt = new byte[] { 0x0A }
        };
        var provider = new DefaultPrivacyOptionsProvider(options);
        var tenantContext = new DefaultTenantContext("tenant-a");
        var hashProvider = new ZoneHashProvider(provider, tenantContext);

        var now = new DateTimeOffset(2026, 2, 7, 10, 0, 0, TimeSpan.Zero);
        var hashA = hashProvider.ComputeZoneHash("Zone-A", "tenant-a", now);
        var hashB = hashProvider.ComputeZoneHash("Zone-A", "tenant-b", now);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void ZoneHashStableWhenAnonymizationDisabled()
    {
        var options = new EcpPrivacyOptions
        {
            EpochDuration = TimeSpan.FromMinutes(1),
            AnonymizeZoneHash = false,
            ZoneHashSalt = Array.Empty<byte>()
        };
        var provider = new DefaultPrivacyOptionsProvider(options);
        var tenantContext = new DefaultTenantContext("tenant-a");
        var hashProvider = new ZoneHashProvider(provider, tenantContext);

        var now = new DateTimeOffset(2026, 2, 7, 10, 0, 0, TimeSpan.Zero);
        var hash1 = hashProvider.ComputeZoneHash("Zone-A", "tenant-a", now);
        var hash2 = hashProvider.ComputeZoneHash("Zone-A", "tenant-a", now.AddMinutes(2));
        var hash3 = hashProvider.ComputeZoneHash("Zone-A", "tenant-b", now);

        Assert.Equal(hash1, hash2);
        Assert.Equal(hash1, hash3);
    }

    [Fact]
    public void ZoneHashChangesAcrossEpochsWithEmptySalt()
    {
        var options = new EcpPrivacyOptions
        {
            EpochDuration = TimeSpan.FromMinutes(1),
            AnonymizeZoneHash = true,
            ZoneHashSalt = ReadOnlyMemory<byte>.Empty
        };
        var provider = new DefaultPrivacyOptionsProvider(options);
        var tenantContext = new DefaultTenantContext("tenant-a");
        var hashProvider = new ZoneHashProvider(provider, tenantContext);

        var now = new DateTimeOffset(2026, 2, 7, 10, 0, 0, TimeSpan.Zero);
        var hash1 = hashProvider.ComputeZoneHash("Zone-A", "tenant-a", now);
        var hash2 = hashProvider.ComputeZoneHash("Zone-A", "tenant-a", now.AddMinutes(2));

        Assert.NotEqual(hash1, hash2);
    }
}
