// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ECP.Core.Tenancy;

namespace ECP.Core.Privacy;

/// <summary>
/// Computes zone hashes with epoch-based anonymization.
/// </summary>
public sealed class ZoneHashProvider
{
    private readonly ITenantPrivacyOptionsProvider _optionsProvider;
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// Creates a zone hash provider.
    /// </summary>
    public ZoneHashProvider(ITenantPrivacyOptionsProvider optionsProvider, ITenantContext tenantContext)
    {
        _optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    /// <summary>
    /// Computes a 16-bit zone hash for the current tenant and time.
    /// </summary>
    public ushort ComputeZoneHash(string rawZone)
    {
        return ComputeZoneHash(rawZone, _tenantContext.TenantId, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Computes a 16-bit zone hash for a tenant and time.
    /// </summary>
    public ushort ComputeZoneHash(string rawZone, string tenantId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(rawZone))
        {
            throw new ArgumentException("Raw zone must be provided.", nameof(rawZone));
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        var options = _optionsProvider.GetOptions(tenantId);
        var zoneBytes = Encoding.UTF8.GetBytes(rawZone);

        if (!options.AnonymizeZoneHash)
        {
            return ComputeTruncatedHash(zoneBytes);
        }

        var epochDuration = options.EpochDuration <= TimeSpan.Zero
            ? TimeSpan.FromMinutes(15)
            : options.EpochDuration;

        var epochSeconds = Math.Max(1, (long)epochDuration.TotalSeconds);
        var epochIndex = now.ToUnixTimeSeconds() / epochSeconds;
        var epochKey = BuildEpochKey(epochIndex, tenantId, options.ZoneHashSalt.Span);

        var combined = new byte[epochKey.Length + zoneBytes.Length];
        epochKey.CopyTo(combined, 0);
        zoneBytes.CopyTo(combined, epochKey.Length);

        return ComputeTruncatedHash(combined);
    }

    /// <summary>
    /// Epoch salt = SHA256(epochIndex || tenantId || baseSalt).
    /// ZoneHash = first 16 bits of SHA256(epochSalt || rawZone).
    /// </summary>
    private static byte[] BuildEpochKey(long epochIndex, string tenantId, ReadOnlySpan<byte> baseSalt)
    {
        var tenantBytes = Encoding.UTF8.GetBytes(tenantId);
        var epochInput = new byte[8 + tenantBytes.Length + baseSalt.Length];
        BinaryPrimitives.WriteInt64BigEndian(epochInput.AsSpan(0, 8), epochIndex);
        tenantBytes.CopyTo(epochInput.AsSpan(8, tenantBytes.Length));
        baseSalt.CopyTo(epochInput.AsSpan(8 + tenantBytes.Length));
        return SHA256.HashData(epochInput);
    }

    private static ushort ComputeTruncatedHash(ReadOnlySpan<byte> data)
    {
        var hash = SHA256.HashData(data);
        return (ushort)((hash[0] << 8) | hash[1]);
    }
}
