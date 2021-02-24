// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        public void EnableReconfiguration() {
            if (ActualStatus == Status.Disabled) {
                _targetStatus = Status.Ready;
            }
        }
    }
}