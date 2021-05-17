// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        /// <summary>
        /// Enables IP reconfiguration.
        /// </summary>
        public void EnableReconfiguration() {
            if (_isInitialized == false) { throw new InvalidOperationException($"Host is not initialized. Call {nameof(EnableReconfiguration)} method first."); }

            if (ActualStatus == Status.Disabled) {
                _targetStatus = Status.Ready;
            }
        }
    }
}
