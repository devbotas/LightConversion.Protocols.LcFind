// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;
using System.Net;
using System.Text;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        private bool TryReadUdpTraffic(out string payload, out IPEndPoint remoteEndpoint) {
            var receiveBuffer = new byte[0x10000]; // <-- This is big enough to hold any UDP packet.

            // This is used as output in ReceiveFrom function.
            EndPoint tempRemoteEndpoint = new IPEndPoint(0, 0);
            payload = "";

            var receivedLength = _listeningSocket.ReceiveFrom(receiveBuffer, ref tempRemoteEndpoint);

            remoteEndpoint = (IPEndPoint)tempRemoteEndpoint;

            var isOk = true;

            if (receivedLength == 0) {
                isOk = false;
                Log.Warn("Message of zero length received");
            }

            if (isOk) {
                try {
                    payload = Encoding.UTF8.GetString(receiveBuffer, 0, receivedLength);
                    Log.Debug($"Received from {remoteEndpoint}: {payload}");
                } catch (Exception ex) {
                    isOk = false;
                    Log.Error(ex, "Skipping this message due to unparsable string");
                }
            }

            return isOk;
        }
    }
}
