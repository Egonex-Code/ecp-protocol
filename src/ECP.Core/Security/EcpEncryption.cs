// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Security.Cryptography;

namespace ECP.Core.Security;

/// <summary>
/// AES-GCM encryption helpers for payloads.
/// </summary>
public static class EcpEncryption
{
    /// <summary>Default nonce length in bytes.</summary>
    public const int NonceSize = 12;
    /// <summary>Default authentication tag length in bytes.</summary>
    public const int TagSize = 16;

    /// <summary>
    /// Encrypts the payload using AES-GCM and returns ciphertext, nonce, and tag.
    /// </summary>
    public static EcpEncryptedPayload Encrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> associatedData = default)
    {
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        return new EcpEncryptedPayload(ciphertext, nonce, tag);
    }

    /// <summary>
    /// Decrypts AES-GCM payload using ciphertext, nonce, and tag.
    /// </summary>
    public static byte[] Decrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> tag,
        ReadOnlySpan<byte> associatedData = default)
    {
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);

        return plaintext;
    }
}

/// <summary>
/// Result of AES-GCM encryption.
/// </summary>
public readonly record struct EcpEncryptedPayload(byte[] Ciphertext, byte[] Nonce, byte[] Tag);
