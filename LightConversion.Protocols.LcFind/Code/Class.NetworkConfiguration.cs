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

        public static bool TryFromRequestString(string requestString, out NetworkConfiguration parsedConfiguration, out string errorMessage) {
            var isOk = true;
            errorMessage = "Ok";
            parsedConfiguration = new NetworkConfiguration();
            var parts = new string[0];

            // Should be nul-terminated.
            if (requestString.Substring(requestString.Length - 1, 1) != "\0") {
                isOk = false;
                errorMessage = "Should be nul-terminated.";
            } else {
                requestString = requestString.TrimEnd('\0');
            }

            if (isOk) {
                // Splitting response into key=value pairs.
                parts = requestString.Split(';');

                // Checking for incorrect pairs.
                foreach (var part in parts) {
                    if (part.Split('=').Length != 2) {
                        isOk = false;
                        errorMessage = "Invalid key-value pair";
                    }
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

                    if (keyValue[0].ToLower() == "ip") {
                        if (IPAddress.TryParse(keyValue[1], out parsedConfiguration.IpAddress) == false) {
                            isOk = false;
                            errorMessage = "Malformed IP address setting";
                        } else {
                            if (CheckIfIpIsNotReserved(parsedConfiguration.IpAddress) == false) {
                                isOk = false;
                                errorMessage = "This IP address is reserved and cannot be used";
                            }
                        }
                    }

                    if (keyValue[0].ToLower() == "mask") {
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

                    if (keyValue[0].ToLower() == "gateway") {
                        if (IPAddress.TryParse(keyValue[1], out parsedConfiguration.GatewayAddress) == false) {
                            isOk = false;
                            errorMessage = "Malformed gateway address setting";
                        }
                    }

                    if (keyValue[0].ToLower() == "hwaddr") {
                        parsedConfiguration.MacAddress = keyValue[1];
                    }
                }
            }

            return isOk;
        }

        public static bool CheckIfIpIsNotReserved(IPAddress newIpAddress) {
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