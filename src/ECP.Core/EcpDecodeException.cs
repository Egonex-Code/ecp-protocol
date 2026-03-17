// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core;

/// <summary>
/// Thrown when ECP decoding fails due to invalid data.
/// </summary>
public sealed class EcpDecodeException : FormatException
{
    /// <summary>
    /// Creates a new decode exception with the specified message.
    /// </summary>
    public EcpDecodeException(string message) : base(message)
    {
    }
}
