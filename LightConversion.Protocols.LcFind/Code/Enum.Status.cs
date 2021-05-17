// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

namespace LightConversion.Protocols.LcFind {
    /// <summary>
    /// LC-FIND host status.
    /// </summary>
    public enum Status {
        Ready,
        AwaitingConfirmation,
        Cooldown,
        Disabled
    }
}
