// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        private async Task ListenForUdpTrafficContinuouslyAsync(CancellationToken cancellationToken) {
            var receiveBuffer = new byte[0x10000]; // <-- This is big enough to hold any UDP packet.

            while (cancellationToken.IsCancellationRequested == false) {
                // This is used as output in ReceiveFrom function.
                EndPoint tempRemoteEndpoint = new IPEndPoint(0, 0);
                string receivedMessage = "";

                var receivedLength = _listeningSocket.ReceiveFrom(receiveBuffer, ref tempRemoteEndpoint);
                var remoteEndpoint = (IPEndPoint)tempRemoteEndpoint;

                var isOk = true;

                if (receivedLength == 0) {
                    isOk = false;
                    Log.Warn("Message of zero length received");
                }

                if (isOk) {
                    try {
                        receivedMessage = Encoding.UTF8.GetString(receiveBuffer, 0, receivedLength);
                    } catch (Exception ex) {
                        isOk = false;
                        Log.Error(ex, "Skipping this message due to unparsable string");
                    }
                }

                if (isOk) {
                    Log.Debug($"Received from {remoteEndpoint}: {receivedMessage}");
                    _remoteEndpoint = remoteEndpoint;
                    var responseNeeded = ProcessMessage(receivedMessage, out var response);
                    if (responseNeeded) {
                        SendResponse(response, remoteEndpoint);
                    }
                }

                await Task.Delay(1, cancellationToken);
            }
        }
    }
}