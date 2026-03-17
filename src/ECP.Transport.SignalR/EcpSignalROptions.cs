// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Transport.Abstractions;

namespace ECP.Transport.SignalR;

/// <summary>
/// Configuration options for the SignalR transport.
/// </summary>
public sealed class EcpSignalROptions : EcpTransportOptions
{
    /// <summary>SignalR method name for sending bytes.</summary>
    public string SendMethodName { get; set; } = "SendEcp";
    /// <summary>SignalR method name for receiving bytes.</summary>
    public string ReceiveMethodName { get; set; } = "ReceiveEcp";
}
