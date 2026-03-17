// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Models;

/// <summary>
/// Action flags encoded in the UET.
/// </summary>
[Flags]
public enum ActionFlags : byte
{
    /// <summary>No actions.</summary>
    None = 0,
    /// <summary>Trigger alarm sound.</summary>
    SoundAlarm = 1 << 0,
    /// <summary>Flash lights.</summary>
    FlashLights = 1 << 1,
    /// <summary>Vibrate device.</summary>
    Vibrate = 1 << 2,
    /// <summary>Play voice instructions.</summary>
    PlayVoice = 1 << 3,
    /// <summary>Show message on screen.</summary>
    ShowMessage = 1 << 4,
    /// <summary>Lock doors.</summary>
    LockDoors = 1 << 5,
    /// <summary>Unlock doors.</summary>
    UnlockDoors = 1 << 6,
    /// <summary>Notify external systems.</summary>
    NotifyExternal = 1 << 7
}
