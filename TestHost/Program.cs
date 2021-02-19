// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;
using System.Net;
using System.Runtime.InteropServices;
using LightConversion.Protocols.LcFind;

namespace TestHost {
    class Program {
        private static string _networkInterface;

        private static NetworkConfiguration _actualConfiguration;

        static void Main(string[] _) {
            _actualConfiguration = new NetworkConfiguration() {
                IpAddress = IPAddress.Parse("172.16.0.11"),
                SubnetMask = IPAddress.Parse("255.255.0.0"),
                GatewayAddress = IPAddress.Parse("172.16.0.1"),
                IsDhcpEnabled = false,
                MacAddress = "01:01:01:01:01:01"
            };

            var lcFindHost = new LcFindHost();

            lcFindHost.DeviceName = "My fancy new PC";
            lcFindHost.SerialNumber = "31315";

            Console.WriteLine($"Setting serial number {lcFindHost.SerialNumber} and device name {lcFindHost.DeviceName}.");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                Console.WriteLine("Running on Linux.");
                _networkInterface = "ethernet0";
            } else {
                Console.WriteLine("Running on Windows.");
                _networkInterface = "Ethernet";
            }

            Console.WriteLine($"Network interface is {_networkInterface}.");

            lcFindHost.Initialize(TrySetFakeNetworkConfiguration, TryGetFakeNetworkConfiguration);
            Console.WriteLine("Initialization completed. Listening.");
            Console.WriteLine("Hit Enter to quit.");

            Console.ReadLine();
        }

        private static bool TrySetFakeNetworkConfiguration(NetworkConfiguration newConfiguration) {
            _actualConfiguration = newConfiguration;

            return true;
        }

        private static bool TryGetFakeNetworkConfiguration(out NetworkConfiguration actualConfiguration) {
            actualConfiguration = _actualConfiguration;

            return true;
        }

        private static bool TrySetNetworkConfiguration(NetworkConfiguration newConfiguration) {
            var isOk = false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                isOk = WindowsNetworkHelpers.ChangeNetworkConfiguration(_networkInterface, newConfiguration);
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                isOk = LinuxNetworkHelpers.ChangeNetworkConfiguration(_networkInterface, newConfiguration);
            }

            return isOk;
        }

        private static bool TryGetActualNetworkConfiguration(out NetworkConfiguration actualConfiguration) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                actualConfiguration = WindowsNetworkHelpers.GetActualNetworkConfiguration(_networkInterface);
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                actualConfiguration = LinuxNetworkHelpers.GetActualNetworkConfiguration(_networkInterface);
            } else {
                actualConfiguration = new NetworkConfiguration();
            }

            return true;
        }
    }
}