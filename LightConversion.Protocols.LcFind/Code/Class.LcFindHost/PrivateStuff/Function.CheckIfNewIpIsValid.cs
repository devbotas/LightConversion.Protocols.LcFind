// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System.Net;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        private bool CheckIfNewIpIsValid(IPAddress newIpAddress) {
            var newIpBytes = newIpAddress.GetAddressBytes();
            var noEmptyAddress = newIpBytes[0] != 0 || newIpBytes[1] != 0 || newIpBytes[2] != 0 || newIpBytes[3] != 0;
            var noLoopback = newIpBytes[0] != 127;
            var noLinkLocal = newIpBytes[0] != 169 || newIpBytes[1] != 254;
            var noTestNet1 = newIpBytes[0] != 192 || newIpBytes[1] != 0;
            var noIpv6Relay = newIpBytes[0] != 192 || newIpBytes[1] != 88 || newIpBytes[2] != 99;
            var noTestNet2 = newIpBytes[0] != 198;
            var noTestNet3 = newIpBytes[0] != 203;
            var noMulticast = newIpBytes[0] != 224;
            var noReserved = newIpBytes[0] != 240;
            var noBroadcast = newIpBytes[0] != 255 || newIpBytes[1] != 255 || newIpBytes[2] != 255 || newIpBytes[3] != 255;

            return noEmptyAddress && noLoopback && noLinkLocal && noTestNet1 && noIpv6Relay && noTestNet2 && noTestNet3 && noMulticast && noReserved && noBroadcast;
        }
    }
}