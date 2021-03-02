// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System.Text;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        private string BuildFindReqResponseString(NetworkConfiguration actualConfiguration) {
            var responseBuilder = new StringBuilder();

            responseBuilder.Append("FIND=1;");
            responseBuilder.Append($"IP={actualConfiguration.IpAddress};");
            responseBuilder.Append($"HWADDR={actualConfiguration.MacAddress};");
            responseBuilder.Append($"DeviceName={DeviceName};");
            responseBuilder.Append($"SN={SerialNumber};");
            responseBuilder.Append($"Status={ActualStatus};");

            if (actualConfiguration.IsDhcpEnabled) {
                responseBuilder.Append("NetworkMode=DHCP;");
            } else {
                responseBuilder.Append("NetworkMode=Static;");
            }

            responseBuilder.Append($"Mask={actualConfiguration.SubnetMask};");
            responseBuilder.Append($"Gateway={actualConfiguration.GatewayAddress};");
            responseBuilder.Append("\0");

            return responseBuilder.ToString();
        }
    }
}