// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;
using System.Net;

namespace LightConversion.Protocols.LcFind {
    public class NetworkConfiguration {
        public bool IsDhcpEnabled;
        public IPAddress IpAddress = IPAddress.None;
        public IPAddress SubnetMask = IPAddress.None;
        public IPAddress GatewayAddress = IPAddress.None;
        public string MacAddress = "";

        public static bool TryFromResponseString(string responseString, out NetworkConfiguration parsedConfiguration, out string errorMessage) {
            var isOk = true;
            errorMessage = "Ok";
            parsedConfiguration = new NetworkConfiguration();

            // Splitting response into key=value pairs.
            var parts = responseString.Split(';');

            // Checking for incorrect pairs.
            foreach (var part in parts) {
                if (part.Split('=').Length != 2) {
                    isOk = false;
                    errorMessage = "Invalid key-value pair";
                }
            }

            // Parsing fields.
            if (isOk) {
                foreach (var part in parts) {
                    var keyValue = part.Split('=');

                    if (keyValue[0].ToLower() == "networkmode") {
                        if (keyValue[1].ToLower() == "dhcp") {
                            parsedConfiguration.IsDhcpEnabled = true;
                        } else if (keyValue[1].ToLower() == "static") {
                            parsedConfiguration.IsDhcpEnabled = false;
                        } else {
                            isOk = false;
                            errorMessage = "Unrecognized network mode setting";
                        }
                    }

                    if (keyValue[0] == "ip") {
                        if (IPAddress.TryParse(keyValue[1], out parsedConfiguration.IpAddress) == false) {
                            isOk = false;
                            errorMessage = "Malformed IP address setting";
                        }
                    }

                    if (keyValue[0] == "mask") {
                        if (IPAddress.TryParse(keyValue[1], out parsedConfiguration.SubnetMask) == false) {
                            isOk = false;
                            errorMessage = "Malformed mask setting";
                        } else {
                            var newMaskBytes = parsedConfiguration.SubnetMask.GetAddressBytes();
                            Array.Reverse(newMaskBytes);
                            var newMask = BitConverter.ToUInt32(newMaskBytes, 0);
                            // Shifting the mask to check if there are no set bits after the first unset bit.
                            while ((newMask & 0x80000000) == 0x80000000) {
                                newMask <<= 1;
                            }

                            if (newMask != 0) {
                                isOk = false;
                                errorMessage = "Malformed mask setting";
                            }
                        }
                    }

                    if (keyValue[0] == "gateway") {
                        if (IPAddress.TryParse(keyValue[1], out parsedConfiguration.GatewayAddress) == false) {
                            isOk = false;
                            errorMessage = "Malformed gateway address setting";
                        }
                    }

                    if (keyValue[0] == "hwaddr") {
                        parsedConfiguration.MacAddress = keyValue[1];
                    }
                }
            }

            return isOk;
        }
    }
}