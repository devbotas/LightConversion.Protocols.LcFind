// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using NLog;

namespace LightConversion.Protocols.LcFind {
    public class LinuxNetworkHelpers {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static bool ChangeNetworkConfiguration(string networkInterfaceName, NetworkConfiguration newConfiguration) {
            var configurationBuilder = new StringBuilder();

            configurationBuilder.AppendLine("[Match]");
            configurationBuilder.AppendLine($"Name={networkInterfaceName}");
            configurationBuilder.AppendLine();
            configurationBuilder.AppendLine("[Network]");

            if (newConfiguration.IsDhcpEnabled) {
                configurationBuilder.AppendLine("DHCP=yes");

                // Allowing device to self-assign an IP address from range 169.254.X.Y.
                // This way device will still be able to receive and send UDP broadcast if DHCP fails. This is default behaviour in Windows, but not in Linux.
                // More info here https://www.freedesktop.org/software/systemd/man/systemd.network.html.
                configurationBuilder.AppendLine("LinkLocalAddressing=yes");
            } else {
                var subnetBitCount = CalculateNumberOfBitsInIpAddress(newConfiguration.SubnetMask);
                configurationBuilder.AppendLine($"Address={newConfiguration.IpAddress}/{subnetBitCount}");

                // TODO: check if received gateway is valid. This is important, because if configured gateway is from different subnet, linux routing table will somehow fuck-up and no broadcasts will be received.
                // if (newConfiguration.IsGatewayValid()) { 
                //  configurationBuilder.AppendLine($"Gateway={newConfiguration.GatewayAddress}");
                //} else {
                configurationBuilder.AppendLine("Gateway=0.0.0.0");
                // }
            }

            File.WriteAllText("/etc/systemd/network/wired.network", configurationBuilder.ToString());

            var exitCode = ExecuteLinuxCommand("systemctl", "restart systemd-networkd").ExitCode;
            if (exitCode == 0) {
                Thread.Sleep(1000);
                return true;
            } else {
                return false;
            }
        }

        private static int CalculateNumberOfBitsInIpAddress(IPAddress ipAddress) {
            var numberOfBits = 0;
            var ipAddressAsUint = BitConverter.ToUInt32(ipAddress.GetAddressBytes());

            while (ipAddressAsUint != 0) {
                numberOfBits++;
                ipAddressAsUint &= (ipAddressAsUint - 1);
            }

            return numberOfBits;
        }


        /*
        Command to execute: ip address show eth0

        Output when cable is unplugged:
        2: eth0: <NO-CARRIER,BROADCAST,MULTICAST,UP> mtu 1500 qdisc mq state DOWN group default qlen 1000
            link/ether 00:14:2d:63:97:eb brd ff:ff:ff:ff:ff:ff
            inet6 fe80::214:2dff:fe63:97eb/64 scope link
               valid_lft forever preferred_lft forever

        Output when DHCP is enabled and IP address is received:
        2: eth0: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc mq state UP group default qlen 1000
            link/ether 00:14:2d:63:97:eb brd ff:ff:ff:ff:ff:ff
            inet 192.168.1.116/24 brd 192.168.1.255 scope global dynamic eth0
               valid_lft 43196sec preferred_lft 43196sec
            inet6 fe80::214:2dff:fe63:97eb/64 scope link
               valid_lft forever preferred_lft forever

        Output when DHCP is enabled and IP address is not received:
        2: eth0: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc mq state UP group default qlen 1000
            link/ether 00:14:2d:63:97:eb brd ff:ff:ff:ff:ff:ff
            inet 169.254.88.49/16 brd 169.254.255.255 scope link eth0
               valid_lft forever preferred_lft forever
            inet6 fe80::214:2dff:fe63:97eb/64 scope link
               valid_lft forever preferred_lft forever

        Output when using static IP:
        2: eth0: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc mq state UP group default qlen 1000
            link/ether 00:14:2d:63:97:eb brd ff:ff:ff:ff:ff:ff
            inet 192.168.244.10/24 brd 192.168.244.255 scope global eth0
               valid_lft forever preferred_lft forever
            inet6 fe80::214:2dff:fe63:97eb/64 scope link
               valid_lft forever preferred_lft forever
        
        Output when using static IP (after few seconds):
        2: eth0: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc mq state UP group default qlen 1000
            link/ether 00:14:2d:63:97:eb brd ff:ff:ff:ff:ff:ff
            inet 169.254.88.49/16 brd 169.254.255.255 scope link eth0
               valid_lft forever preferred_lft forever
            inet 192.168.244.10/24 brd 192.168.244.255 scope global eth0
               valid_lft forever preferred_lft forever
            inet6 fe80::214:2dff:fe63:97eb/64 scope link
               valid_lft forever preferred_lft forever
        
        To get gateway address:
        colibri-imx7-06526955:~/debug$ ip r
        default via 192.168.1.254 dev eth0 proto dhcp src 192.168.1.117 metric 1024
        default dev eth0 proto static scope link metric 2048
        169.254.0.0/16 dev eth0 proto kernel scope link src 169.254.88.49
        172.17.0.0/16 dev docker0 proto kernel scope link src 172.17.0.1
        172.18.0.0/16 dev br-30bfa95bff51 proto kernel scope link src 172.18.0.1 linkdown
        192.168.1.0/24 dev eth0 proto kernel scope link src 192.168.1.117
        192.168.1.254 dev eth0 proto dhcp scope link src 192.168.1.117 metric 1024

        */
        public static NetworkConfiguration GetActualNetworkConfiguration(string networkInterfaceName) {
            var actualConfig = new NetworkConfiguration();

            var ipAddressCommandResult = ExecuteLinuxCommand("ip", "address show " + networkInterfaceName);

            var macAddressRegex = new Regex("link/ether (..:..:..:..:..:..) brd");
            var macAddressMatch = macAddressRegex.Match(ipAddressCommandResult.Output);

            if (macAddressMatch.Success) {
                actualConfig.MacAddress = macAddressMatch.Groups[1].Value;
            } else {
                Log.Error($"Parsing MAC from output \"{ipAddressCommandResult.Output}\" failed.");
            }

            var dhcpAddressRegex = new Regex(@"inet ([0-9]*\.[0-9]*.[0-9]*.[0-9]*)\/(..).*scope global dynamic " + networkInterfaceName);
            var dhcpAddressMatch = dhcpAddressRegex.Match(ipAddressCommandResult.Output);

            if (dhcpAddressMatch.Success) {
                actualConfig.IsDhcpEnabled = true;
                actualConfig.IpAddress = IPAddress.Parse(dhcpAddressMatch.Groups[1].Value);

                var subnetBitCount = int.Parse(dhcpAddressMatch.Groups[2].Value);
                actualConfig.SubnetMask = ConvertBitsCountToAddress(subnetBitCount);
            } else {
                var staticAddressRegex = new Regex(@"inet ([0-9]*\.[0-9]*.[0-9]*.[0-9]*)\/(..).*scope global " + networkInterfaceName);
                var staticAddressMatch = staticAddressRegex.Match(ipAddressCommandResult.Output);

                if (staticAddressMatch.Success) {
                    actualConfig.IsDhcpEnabled = false;
                    actualConfig.IpAddress = IPAddress.Parse(staticAddressMatch.Groups[1].Value);

                    var subnetBitCount = int.Parse(staticAddressMatch.Groups[2].Value);
                    actualConfig.SubnetMask = ConvertBitsCountToAddress(subnetBitCount);
                } else {
                    var dhcpFallbackAddressRegex = new Regex(@"inet ([0-9]*\.[0-9]*.[0-9]*.[0-9]*)\/(..).*scope link " + networkInterfaceName);
                    var dhcpFallbackAddressMatch = dhcpFallbackAddressRegex.Match(ipAddressCommandResult.Output);

                    if (dhcpFallbackAddressMatch.Success) {
                        actualConfig.IsDhcpEnabled = true;
                        actualConfig.IpAddress = IPAddress.Parse(dhcpFallbackAddressMatch.Groups[1].Value);

                        var subnetBitCount = int.Parse(dhcpFallbackAddressMatch.Groups[2].Value);
                        actualConfig.SubnetMask = ConvertBitsCountToAddress(subnetBitCount);
                    }
                }
            }

            var iprCommandOutput = ExecuteLinuxCommand("ip", "r");
            var gatewayRegex = new Regex(@"default via ([0-9]*\.[0-9]*.[0-9]*.[0-9]*) dev " + networkInterfaceName);
            var gatewayMatch = gatewayRegex.Match(iprCommandOutput.Output);

            if (gatewayMatch.Success) {
                actualConfig.GatewayAddress = IPAddress.Parse(gatewayMatch.Groups[1].Value);
            } else {
                actualConfig.GatewayAddress = IPAddress.Parse("0.0.0.0");
            }

            return actualConfig;
        }

        private static IPAddress ConvertBitsCountToAddress(int bitCount) {
            byte[] addressBytes = { 0xFF, 0xFF, 0xFF, 0xFF };
            var emptyBytes = (32 - bitCount) / 8;
            var emptyBits = (32 - bitCount) % 8;
            for (var i = 3; i >= (4 - emptyBytes); i--) {
                addressBytes[i] = 0;
            }

            if (emptyBytes < 4) {
                addressBytes[3 - emptyBytes] <<= emptyBits;
            }

            var ipAddress = new IPAddress(addressBytes);
            return ipAddress;
        }

        public static (int ExitCode, string Output) ExecuteLinuxCommand(string fileName, string arguments) {
            int exitCode;
            string output;

            using (var process = new Process()) {
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;

                try {
                    process.Start();
                    output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                } catch (Exception ex) {
                    Log.Error(ex, $"Starting process \"{fileName}\" with arguments \"{arguments}\" failed with exception. Probably such linux command doesn't exist.");
                    exitCode = -1;
                    output = "";
                }
            }

            return (exitCode, output);
        }
    }
}