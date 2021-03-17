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
            public string Status { get; set; }

            public string LookerNetworkInterfaceName { get; set; }
            public string LookerIpAddress { get; set; }
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

        private static DeviceDescription ParseDeviceDescriptionFromString(string message) {
            // A reusable helper.
            static string GetParameterFromSeggerString(string seggerString, string parameterName) {
                var parts = seggerString.Split(';');

                for (int i = 0; i < parts.Length; i++) {
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