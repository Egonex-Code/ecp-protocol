// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Security;

/// <summary>
/// Provides HMAC keys by key version.
/// </summary>
public interface IKeyProvider
{
    /// <summary>
    /// Attempts to retrieve the key for a specific key version.
    /// </summary>
    bool TryGetKey(byte keyVersion, out ReadOnlyMemory<byte> key);
}
