// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System.Text;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        private string BuildConfReqResponseString(string requestResult) {
            var returnString = "";
            var responseBuilder = new StringBuilder();

            responseBuilder.Append("CONF=1;");
#warning _hwaddress should become a property maybe?..
            responseBuilder.Append($"HWADDR={_hwAddress};");
            responseBuilder.Append($"Status={ActualStatus};");
            responseBuilder.Append($"Result={requestResult};");


#warning What is the point in getting configuration, when, at this point, settings reported OK?
            if (_tryGetNetworkConfigurationDelegate(out var actualConfig)) {
                if (actualConfig.IsDhcpEnabled) {
                    responseBuilder.Append("NetworkMode=DHCP;");
#warning Shall we not include IP addresses and stuff here?..
                } else {
                    responseBuilder.Append("NetworkMode=Static;");
                    responseBuilder.Append($"IP={actualConfig.IpAddress};");
                    responseBuilder.Append($"Mask={actualConfig.SubnetMask};");
                    responseBuilder.Append($"Gateway={actualConfig.GatewayAddress};");
                }
            }

            responseBuilder.Append("\0");
            returnString = responseBuilder.ToString();

            return returnString;
        }
    }
}