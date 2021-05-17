// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        /// <summary>
        /// Confirms IP configuration change. If <see cref="ActualStatus"/> is not <see cref="Status.AwaitingConfirmation"/>, this method will have no effect.
        /// </summary>
        public void Confirm() {
            if (_isInitialized == false) { throw new InvalidOperationException($"Host is not initialized. Call {nameof(Initialize)} method first."); }

            if (ActualStatus == Status.AwaitingConfirmation) {
                _targetStatus = Status.Cooldown;
            }
        }
    }
}
