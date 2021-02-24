// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System.Text;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        private bool ProcessMessage(string receivedMessage, out string responseMessage) {
            var returnValue = false;
            responseMessage = "";

            var responseBuilder = new StringBuilder();
            if (receivedMessage.StartsWith("FINDReq=1;")) {
                if (_tryGetNetworkConfigurationDelegate(out var actualConfig)) {
                    responseBuilder.Append("FIND=1;");
                    responseBuilder.Append($"IP={actualConfig.IpAddress};");
                    responseBuilder.Append($"HWADDR={actualConfig.MacAddress};");
                    responseBuilder.Append($"DeviceName={DeviceName};");
                    responseBuilder.Append($"SN={SerialNumber};");
                    responseBuilder.Append($"Status={ActualStatus};");

                    if (actualConfig.IsDhcpEnabled) {
                        responseBuilder.Append("NetworkMode=DHCP;");
                    } else {
                        responseBuilder.Append("NetworkMode=Static;");
                    }

                    responseBuilder.Append($"Mask={actualConfig.SubnetMask};");
                    responseBuilder.Append($"Gateway={actualConfig.GatewayAddress};");
                    responseBuilder.Append("\0");

                    responseMessage = responseBuilder.ToString();
                    returnValue = true;
                } else {
                    Log.Error("Could not retrieve actual network configuration, and so cannot send a proper response to FINDReq request.");
                }
            }

            if (IsReconfigurationEnabled && receivedMessage.StartsWith($"CONFReq=1;HWADDR={_hwAddress};")) {
                var isOk = NetworkConfiguration.TryFromResponseString(receivedMessage, out var receivedConfiguration, out var requestResult);

                if (isOk) {
                    if (CheckIfNewIpIsValid(receivedConfiguration.IpAddress) == false) {
                        isOk = false;
                        requestResult = "Cannot use this IP address";
                    }
                }

                if (isOk) {
                    _configurationToSet = receivedConfiguration;

                    if (IsConfirmationEnabled) {
                        _targetStatus = Status.AwaitingConfirmation;
                    } else {
                        _targetStatus = Status.Cooldown;
                    }

                    //if (_trySetNetworkConfigurationDelegate(receivedConfiguration)) {
                    //    _cooldownCounter = CooldownTimeout;
                    //    requestResult = "Ok";
                    //    ActualStatus = Status.Cooldown;
                    //} else {
                    //    requestResult = "Unable to set requested configuration";
                    //    ActualStatus = Status.Ready;
                    //}
                } else {
#warning this global response... Do I have to send it here, on error?
                    responseMessage = BuildConfReqResponseString(requestResult);
                    returnValue = true;
                }

                // response = new Response(true, true, BuildConfReqResponseString(requestResult));
            }

            return returnValue;
        }
    }
}