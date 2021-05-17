// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LightConversion.Protocols.LcFind {
    /// <summary>
    /// Provides LC-FIND client-side functionality.
    /// </summary>
    public class LcFindClient {
        /// <summary>
        /// Searches for the available devices on the network.
        /// </summary>
        /// <param name="localAddress">IP address to search from. This IP address basically corresponds to a network adapter. There may be many networks adapters in the systems, some of them being virtual or otherwise unusable for LC-FIND. Look in helper calsses for inspiration of how to distinguish "bad" adapters from "good" ones. </param>
        public static List<DeviceDescription> LookForDevices(IPAddress localAddress) {
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

                while (socket.Available > 0) {
                    EndPoint remoteEndpoint = new IPEndPoint(0, 0);
                    var messageLength = socket.ReceiveFrom(receiveBuffer, ref remoteEndpoint);
                    var message = Encoding.UTF8.GetString(receiveBuffer, 0, messageLength);

                    var deviceDescription = DeviceDescription.FromString(message);
                    if (string.IsNullOrEmpty(deviceDescription.SerialNumber) == false) {
                        if (deviceDescriptions.Any(d => d.SerialNumber == deviceDescription.SerialNumber) == false) {
                            deviceDescriptions.Add(deviceDescription);
                        }
                    }
                }
            }

            return deviceDescriptions;
        }
    }
}
