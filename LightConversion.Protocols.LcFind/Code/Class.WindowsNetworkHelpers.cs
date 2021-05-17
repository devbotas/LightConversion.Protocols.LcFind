// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NLog;

namespace LightConversion.Protocols.LcFind {
    /// <summary>
    /// A helper class for Windows platform.
    /// </summary>
    public class WindowsNetworkHelpers {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static bool ChangeNetworkConfiguration(string networkInterfaceName, NetworkConfiguration newConfiguration) {
            bool isOk;

            if (newConfiguration.IsDhcpEnabled) {
                using (var process = new Process()) {
                    process.StartInfo.FileName = "netsh";
                    process.StartInfo.Arguments = $"interface ip set address \"{networkInterfaceName}\" dhcp";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;

                    _log.Debug($"Executing command: netsh {process.StartInfo.Arguments}");
                    process.Start();

                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    isOk = process.ExitCode == 0;

                    _log.Debug($"Output: {output}");
                    _log.Debug($"Exit code: {process.ExitCode}");
                }
            } else {
                using (var process = new Process()) {
                    process.StartInfo.FileName = "netsh";
                    process.StartInfo.Arguments = $"interface ip set address \"{networkInterfaceName}\" static {newConfiguration.IpAddress} {newConfiguration.SubnetMask}";
                    if (newConfiguration.GatewayAddress != null) {
                        process.StartInfo.Arguments += $" {newConfiguration.GatewayAddress} 1";
                    }

                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;

                    _log.Debug($"Executing command: netsh {process.StartInfo.Arguments}");
                    process.Start();

                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    isOk = process.ExitCode == 0;

                    _log.Debug($"Output: {output}");
                    _log.Debug($"Exit code: {process.ExitCode}");
                }
            }

            return isOk;
        }

        public static NetworkConfiguration GetActualNetworkConfiguration(string networkInterfaceName) {
            var networkConfiguration = new NetworkConfiguration();

            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            var relevantNetworkInterface = networkInterfaces.FirstOrDefault(n => n.Name == networkInterfaceName);

            if (relevantNetworkInterface != null) {
                var ipProperties = relevantNetworkInterface.GetIPProperties();
                var ipV4Properties = ipProperties.GetIPv4Properties();

                networkConfiguration.IsDhcpEnabled = ipV4Properties.IsDhcpEnabled;

                if (ipProperties.GatewayAddresses.Count > 0) {
                    networkConfiguration.GatewayAddress = ipProperties.GatewayAddresses[0].Address;
                } else {
                    networkConfiguration.GatewayAddress = IPAddress.Parse("0.0.0.0");
                }

                var macAddressBytes = relevantNetworkInterface.GetPhysicalAddress().GetAddressBytes();
                networkConfiguration.MacAddress = $"{macAddressBytes[0]:X2}:{macAddressBytes[1]:X2}:{macAddressBytes[2]:X2}:{macAddressBytes[3]:X2}:{macAddressBytes[4]:X2}:{macAddressBytes[5]:X2}";

                var relevantUnicastIpAddress = ipProperties.UnicastAddresses.FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);

                if (relevantUnicastIpAddress != null) {
                    networkConfiguration.IpAddress = relevantUnicastIpAddress.Address;
                    networkConfiguration.SubnetMask = relevantUnicastIpAddress.IPv4Mask;
                } else {
                    networkConfiguration.IpAddress = IPAddress.Parse("0.0.0.0");
                    networkConfiguration.SubnetMask = IPAddress.Parse("0.0.0.0");
                }
            }

            return networkConfiguration;
        }
    }
}
