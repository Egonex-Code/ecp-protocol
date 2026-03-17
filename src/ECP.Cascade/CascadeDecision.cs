// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Envelope;

namespace ECP.Cascade;

/// <summary>
/// Result of evaluating a cascade forwarding decision.
/// </summary>
public readonly record struct CascadeDecision(
    bool ShouldForward,
    EmergencyEnvelope ForwardedEnvelope,
    int FanOutLimit,
    string Reason)
{
    /// <summary>
    /// Creates a rejected decision with a reason.
    /// </summary>
    public static CascadeDecision Reject(string reason) =>
        new(false, default, 0, reason);

    /// <summary>
    /// Creates a forward decision with envelope and fan-out.
    /// </summary>
    public static CascadeDecision Forward(EmergencyEnvelope envelope, int fanOutLimit, string reason) =>
        new(true, envelope, fanOutLimit, reason);
}
