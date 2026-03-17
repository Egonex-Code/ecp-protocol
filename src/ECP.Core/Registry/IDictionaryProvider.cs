// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Registry;

/// <summary>
/// Provides dictionary-based compression and decompression.
/// </summary>
public interface IDictionaryProvider
{
    /// <summary>
    /// Attempts to compress the input into the provided output buffer.
    /// </summary>
    bool TryCompress(ReadOnlySpan<byte> input, Span<byte> output, out int bytesWritten);

    /// <summary>
    /// Attempts to decompress the input into the provided output buffer.
    /// </summary>
    bool TryDecompress(ReadOnlySpan<byte> input, Span<byte> output, out int bytesWritten);

    /// <summary>Dictionary identifier.</summary>
    byte DictionaryId { get; }

    /// <summary>Dictionary version.</summary>
    byte DictionaryVersion { get; }
}
