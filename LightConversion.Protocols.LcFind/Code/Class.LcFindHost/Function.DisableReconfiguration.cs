// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        /// <summary>
        /// Disables IP address reconfiguration. FINDReq messages will still be processed.
        /// </summary>
        public void DisableReconfiguration() {
            if (_isInitialized == false) { throw new InvalidOperationException($"Host is not initialized. Call {nameof(DisableReconfiguration)} method first."); }

            _targetStatus = Status.Disabled;
        }
    }
}
