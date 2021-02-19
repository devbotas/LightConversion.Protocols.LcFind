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

namespace LightConversion.Protocols.LcFind {
    public class LcFindClient {
        public class DeviceDescription {
            public string SerialNumber { get; set; }
            public string MacAddress { get; set; }
            public string DeviceName { get; set; }
            public string NetworkMode { get; set; }
            public string IpAddress { get; set; }
            public string GatewayAddress { get; set; }
            public string SubnetMask { get; set; }
            public bool IsReachable { get; set; }

            public string LookerNetworkInterfaceName { get; set; }
            public string LookerIpAddress { get; set; }
        }

        public static List<DeviceDescription> LookForDevices() {
            var deviceDescriptions = new List<DeviceDescription>();

            var localIpAddresses = GetAllLocalIpAddresses();

            foreach (var localIpAddress in localIpAddresses) {
                deviceDescriptions.AddRange(LookForDevices(localIpAddress.NetworkInterface, localIpAddress.IpAddress));
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

        public static List<DeviceDescription> LookForDevices(string networkInterfaceName, IPAddress localAddress) {
            var deviceDescriptions = new List<DeviceDescription>();

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)) {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontRoute, true);

                // Need to bind, otherwise broadcasts won't be catch and only reachable devices could be detected.
                try {
                    // Previously I was binding to IPAddress.Any and it wasn't always working because it doesn't guarantee that correct network adapter will be used for sending broadcasts.
                    // Therefore I am now explicitly binding to local IP address of known adapter. This way I am almost sure that broadcast is coming out from that adapter only.
                    socket.Bind(new IPEndPoint(localAddress, 50022));
                } catch (SocketException ex) {
                    // TODO: logging.
                    // May fail, if another app is already using this port without "ReuseAddress" flag.
                    Debug.Print(ex.ToString());
                }

                var receiveBuffer = new byte[65535];

                var messageToSend = Encoding.UTF8.GetBytes("FINDReq=1;\0");
                socket.SendTo(messageToSend, new IPEndPoint(IPAddress.Broadcast, 50022));

                // Allowing some time for messages to come.
                Thread.Sleep(1000);

                using (var ping = new Ping()) {
                    while (socket.Available > 0) {
                        EndPoint remoteEndpoint = new IPEndPoint(0, 0);
                        var messageLength = socket.ReceiveFrom(receiveBuffer, ref remoteEndpoint);
                        var message = Encoding.UTF8.GetString(receiveBuffer, 0, messageLength);

                        var deviceDescription = ParseDeviceDescriptionFromString(message);
                        if (string.IsNullOrEmpty(deviceDescription.SerialNumber) == false) {
                            if (deviceDescriptions.Any(d => d.SerialNumber == deviceDescription.SerialNumber) == false) {
                                var remoteIpEndPoint = (IPEndPoint)remoteEndpoint;
                                deviceDescription.IpAddress = remoteIpEndPoint.Address.ToString();
                                deviceDescription.LookerIpAddress = localAddress.ToString();
                                deviceDescription.LookerNetworkInterfaceName = networkInterfaceName;

                                deviceDescriptions.Add(deviceDescription);

                                try {
                                    var pingResult = ping.Send(remoteIpEndPoint.Address, 100);
                                    if (pingResult != null && pingResult.Status == IPStatus.Success) {
                                        deviceDescription.IsReachable = true;
                                    }
                                } catch (PingException ex) {
                                    Debug.Print(ex.ToString());
                                }
                            }
                        }
                    }
                }
            }

            return deviceDescriptions;
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

        private static DeviceDescription ParseDeviceDescriptionFromString(string message) {
            var parseResult = new DeviceDescription();

            parseResult.SerialNumber = GetParameterFromSeggerString(message, "SN");
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