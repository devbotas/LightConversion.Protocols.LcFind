// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System.Text;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        private string BuildFindReqResponseString(NetworkConfiguration actualConfigrutaion) {
            var responseBuilder = new StringBuilder();

            responseBuilder.Append("FIND=1;");
            responseBuilder.Append($"IP={actualConfigrutaion.IpAddress};");
            responseBuilder.Append($"HWADDR={actualConfigrutaion.MacAddress};");
            responseBuilder.Append($"DeviceName={DeviceName};");
            responseBuilder.Append($"SN={SerialNumber};");
            responseBuilder.Append($"Status={ActualStatus};");

            if (actualConfigrutaion.IsDhcpEnabled) {
                responseBuilder.Append("NetworkMode=DHCP;");
            } else {
                responseBuilder.Append("NetworkMode=Static;");
            }

            responseBuilder.Append($"Mask={actualConfigrutaion.SubnetMask};");
            responseBuilder.Append($"Gateway={actualConfigrutaion.GatewayAddress};");
            responseBuilder.Append("\0");

            return responseBuilder.ToString();
        }
    }
}