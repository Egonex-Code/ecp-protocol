// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Security.Cryptography;
using ECP.Core.Models;
using ECP.Core.Security;
using Xunit;

namespace ECP.Core.Tests;

public class SecurityTests
{
    [Fact]
    public void ComputeHmacReturnsRequestedLength()
    {
        var key = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var data = new byte[] { 9, 10, 11 };

        var hmac0 = EcpSecurity.ComputeHmac(key, data, 0);
        var hmac8 = EcpSecurity.ComputeHmac(key, data, 8);
        var hmac12 = EcpSecurity.ComputeHmac(key, data, 12);
        var hmac16 = EcpSecurity.ComputeHmac(key, data, 16);

        Assert.Empty(hmac0);
        Assert.Equal(8, hmac8.Length);
        Assert.Equal(12, hmac12.Length);
        Assert.Equal(16, hmac16.Length);
    }

    [Fact]
    public void VerifyHmacAcceptsCorrectKey()
    {
        var key = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var hmac = EcpSecurity.ComputeHmac(key, data, 12);

        Assert.True(EcpSecurity.VerifyHmac(key, data, hmac));
    }

    [Fact]
    public void VerifyHmacAcceptsEmptyWhenLengthIsZero()
    {
        var key = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        var data = new byte[] { 0x01, 0x02, 0x03 };

        Assert.True(EcpSecurity.VerifyHmac(key, data, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void VerifyHmacRejectsWrongKey()
    {
        var key = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        var wrongKey = new byte[] { 0x00, 0x11, 0x22, 0x33 };
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var hmac = EcpSecurity.ComputeHmac(key, data, 12);

        Assert.False(EcpSecurity.VerifyHmac(wrongKey, data, hmac));
    }

    [Fact]
    public void EncryptDecryptRoundtrip()
    {
        var key = new byte[32];
        for (var i = 0; i < key.Length; i++)
        {
            key[i] = (byte)(i + 1);
        }

        var plaintext = new byte[] { 0x10, 0x20, 0x30, 0x40 };
        var encrypted = EcpEncryption.Encrypt(key, plaintext);
        var decrypted = EcpEncryption.Decrypt(key, encrypted.Ciphertext, encrypted.Nonce, encrypted.Tag);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void DecryptWithWrongKeyThrows()
    {
        var key = new byte[32];
        var wrongKey = new byte[32];
        Array.Fill(key, (byte)0x01);
        Array.Fill(wrongKey, (byte)0x02);

        var plaintext = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var encrypted = EcpEncryption.Encrypt(key, plaintext);

        Assert.ThrowsAny<CryptographicException>(() =>
            EcpEncryption.Decrypt(wrongKey, encrypted.Ciphertext, encrypted.Nonce, encrypted.Tag));
    }

    [Fact]
    public void AntiReplayRejectsTimestampOutsideWindow()
    {
        var window = new AntiReplayWindow();
        var now = DateTimeOffset.UtcNow;
        var oldTimestamp = now.AddMinutes(-10);

        var ok = window.TryAccept(1, oldTimestamp, ttlSeconds: 10, now, out _);

        Assert.False(ok);
    }

    [Fact]
    public void AntiReplayRejectsDuplicateMessageId()
    {
        var window = new AntiReplayWindow();
        var now = DateTimeOffset.UtcNow;

        var first = window.TryAccept(42, now, ttlSeconds: 10, now, out _);
        var second = window.TryAccept(42, now, ttlSeconds: 10, now, out _);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void AntiReplayIsolatedPerTenant()
    {
        var window = new AntiReplayWindow();
        var now = DateTimeOffset.UtcNow;

        var first = window.TryAccept("tenant-a", 42, now, ttlSeconds: 10, now, out _);
        var second = window.TryAccept("tenant-b", 42, now, ttlSeconds: 10, now, out _);

        Assert.True(first);
        Assert.True(second);
    }

    [Fact]
    public void KeyRingReturnsKeyByVersion()
    {
        var ring = new KeyRing();
        var key = new byte[] { 0x01, 0x02, 0x03 };

        ring.AddKey(5, key);

        var ok = ring.TryGetKey(5, out var retrieved);

        Assert.True(ok);
        Assert.Equal(key, retrieved.ToArray());
    }

    [Fact]
    public void KeyRingIsolatedPerTenant()
    {
        var ring = new KeyRing();
        var keyA = new byte[] { 0x01, 0x02, 0x03 };
        var keyB = new byte[] { 0x0A, 0x0B, 0x0C };

        ring.AddKey("tenant-a", 1, keyA);
        ring.AddKey("tenant-b", 1, keyB);

        Assert.True(ring.TryGetKey("tenant-a", 1, out var retrievedA));
        Assert.True(ring.TryGetKey("tenant-b", 1, out var retrievedB));
        Assert.Equal(keyA, retrievedA.ToArray());
        Assert.Equal(keyB, retrievedB.ToArray());
    }

    [Fact]
    public void ConfirmHashIs18Bits()
    {
        var key = new byte[] { 0x10, 0x11, 0x12, 0x13 };
        var hash = EcpSecurity.ComputeConfirmHash(
            key,
            messageId: 0x0102030405060708,
            timestampMinutes: 0x1234,
            zoneHash: 0xABCD,
            emergencyType: EmergencyType.Fire,
            priority: EcpPriority.Critical,
            actionFlags: ActionFlags.SoundAlarm);

        Assert.True(hash <= 0x3FFFF);
    }
}
