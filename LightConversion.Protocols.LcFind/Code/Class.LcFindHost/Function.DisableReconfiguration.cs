// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        /// <summary>
        /// Disables IP address reconfiguration. FINDReq messages will still be processed.
        /// </summary>
        public void DisableReconfiguration() {
            _targetStatus = Status.Disabled;
        }
    }
}
