// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using LightConversion.Protocols.LcFind;

namespace TestClient {
    public class DeviceFinder {
        public static List<LcFindClient.DeviceDescription> LookForDevices() {
            var deviceDescriptions = new List<LcFindClient.DeviceDescription>();

            var localIpAddresses = GetAllLocalIpAddresses();

            foreach (var localIpAddress in localIpAddresses) {
                deviceDescriptions.AddRange(LcFindClient.LookForDevices(localIpAddress.NetworkInterface, localIpAddress.IpAddress));
            }

            return deviceDescriptions;
        }

        public static List<(string NetworkInterface, IPAddress IpAddress)> GetAllLocalIpAddresses() {
            var localIpAddresses = new List<(string NetworkInterface, IPAddress IpAddress)>();

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces()) {
                var isValidInterface = networkInterface.OperationalStatus == OperationalStatus.Up;
                isValidInterface &= networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback;

                if (isValidInterface) {
                    var ipProperties = networkInterface.GetIPProperties();

                    var relevantUnicastIpAddress = ipProperties.UnicastAddresses.FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);

                    if (relevantUnicastIpAddress != null) {
                        localIpAddresses.Add((networkInterface.Name, relevantUnicastIpAddress.Address));
                    }
                } else {
                    Debug.Print($"Skipping interface \"{networkInterface.Name}\".");
                }
            }

            return localIpAddresses;
        }

        public static void ReconfigureDeviceWithStaticIp(string actualMacAddress, string localAdapterAddress, string newIpAddress, string newSubnetMask, string newGatewayAddress) {
            try {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)) {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

                    socket.Bind(new IPEndPoint(IPAddress.Parse(localAdapterAddress), 50022));

                    var messageToSend = Encoding.UTF8.GetBytes($"CONFReq=1;HWADDR={actualMacAddress};NetworkMode=Static;IP={newIpAddress};Mask={newSubnetMask};Gateway={newGatewayAddress}\0");
                    socket.SendTo(messageToSend, new IPEndPoint(IPAddress.Broadcast, 50022));
                }
            } catch (SocketException ex) {
                // TODO: logging.
                Debug.Print(ex.ToString());
            }
        }

        public static void ReconfigureDeviceWithDhcp(string actualMacAddress, string localAdapterAddress) {
            try {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)) {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

                    socket.Bind(new IPEndPoint(IPAddress.Parse(localAdapterAddress), 50022));

                    var messageToSend = Encoding.UTF8.GetBytes($"CONFReq=1;HWADDR={actualMacAddress};NetworkMode=DHCP\0");
                    socket.SendTo(messageToSend, new IPEndPoint(IPAddress.Broadcast, 50022));
                }
            } catch (SocketException ex) {
                // TODO: logging.
                Debug.Print(ex.ToString());
            }
        }

        private static LcFindClient.DeviceDescription ParseDeviceDescriptionFromString(string message) {
            var parseResult = new LcFindClient.DeviceDescription();

            parseResult.SerialNumber = GetParameterFromSeggerString(message, "SN");
            parseResult.IpAddress = GetParameterFromSeggerString(message, "IP");
            parseResult.MacAddress = GetParameterFromSeggerString(message, "HWADDR");
            parseResult.DeviceName = GetParameterFromSeggerString(message, "DeviceName");
            parseResult.NetworkMode = GetParameterFromSeggerString(message, "NetworkMode");
            parseResult.SubnetMask = GetParameterFromSeggerString(message, "Mask");
            parseResult.GatewayAddress = GetParameterFromSeggerString(message, "Gateway");

            return parseResult;
        }

        private static string GetParameterFromSeggerString(string seggerString, string parameterName) {
            var parts = seggerString.Split(';');

            for (int i = 0; i < parts.Length; i++) {
                if (parts[i].StartsWith($"{parameterName}=")) {
                    return parts[i].Substring(parameterName.Length + 1).Trim('\0', ' ', '\r', '\n');
                }
            }

            return "";
        }
    }
}