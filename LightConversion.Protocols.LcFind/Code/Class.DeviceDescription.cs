// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

namespace LightConversion.Protocols.LcFind {
    public class DeviceDescription {
        public string SerialNumber { get; set; }
        public string MacAddress { get; set; }
        public string DeviceName { get; set; }
        public string NetworkMode { get; set; }
        public string IpAddress { get; set; }
        public string GatewayAddress { get; set; }
        public string SubnetMask { get; set; }
        public string Status { get; set; }

        public static DeviceDescription FromString(string message) {
            // A reusable helper.
            static string GetParameterFromSeggerString(string seggerString, string parameterName) {
                var parts = seggerString.Split(';');

                for (var i = 0; i < parts.Length; i++) {
                    if (parts[i].StartsWith($"{parameterName}=")) {
                        return parts[i].Substring(parameterName.Length + 1).Trim('\0', ' ', '\r', '\n');
                    }
                }

                return "";
            }

            var parseResult = new DeviceDescription();

            parseResult.SerialNumber = GetParameterFromSeggerString(message, "SN");
            parseResult.IpAddress = GetParameterFromSeggerString(message, "IP");
            parseResult.MacAddress = GetParameterFromSeggerString(message, "HWADDR");
            parseResult.DeviceName = GetParameterFromSeggerString(message, "DeviceName");
            parseResult.NetworkMode = GetParameterFromSeggerString(message, "NetworkMode");
            parseResult.SubnetMask = GetParameterFromSeggerString(message, "Mask");
            parseResult.GatewayAddress = GetParameterFromSeggerString(message, "Gateway");
            parseResult.Status = GetParameterFromSeggerString(message, "Status");

            return parseResult;
        }
    }
}
