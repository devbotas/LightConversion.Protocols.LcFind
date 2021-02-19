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

        public static bool TryFromResponseString(string responseString, out NetworkConfiguration parsedConfiguration) {
            var isOk = true;
            parsedConfiguration = new NetworkConfiguration();

            // Splitting response into key=value pairs.
            var parts = responseString.Split(';');

            // Checking for incorrect pairs.
            foreach (var part in parts) {
                if (part.Split('=').Length != 2) {
                    isOk = false;
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
                            isOk &= false;
                        }
                    }

                    if (keyValue[0] == "ip") {
                        isOk &= IPAddress.TryParse(keyValue[1], out parsedConfiguration.IpAddress);
                    }

                    if (keyValue[0] == "mask") {
                        isOk &= IPAddress.TryParse(keyValue[1], out parsedConfiguration.SubnetMask);
                    }

                    if (keyValue[0] == "gateway") {
                        isOk &= IPAddress.TryParse(keyValue[1], out parsedConfiguration.GatewayAddress);
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