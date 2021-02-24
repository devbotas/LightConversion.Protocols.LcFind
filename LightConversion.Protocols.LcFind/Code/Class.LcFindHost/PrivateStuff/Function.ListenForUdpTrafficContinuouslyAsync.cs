// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;
using System.Net;
using System.Net.Sockets;
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

                var receivedLength = _listeningSocket.ReceiveFrom(receiveBuffer, ref tempRemoteEndpoint);
                var remoteEndpoint = (IPEndPoint)tempRemoteEndpoint;

                if (receivedLength > 0) {
                    string receivedMessage = null;
                    try {
                        receivedMessage = Encoding.UTF8.GetString(receiveBuffer, 0, receivedLength);
                    } catch (Exception ex) {
                        Log.Error(ex, "Skipping this message due to unparsable string");
                    }

                    Log.Debug($"Received from {remoteEndpoint}: {receivedMessage}");

#warning It is neither null nor empty at this point?
                    if (string.IsNullOrEmpty(receivedMessage) == false) {
                        _remoteEndpoint = remoteEndpoint;
                        var responseNeeded = ProcessMessage(receivedMessage, out var response);
                        if (responseNeeded) {
                            SendResponse(response, remoteEndpoint);
                        }
                    }
                } else {
                    Log.Warn("Message of zero length received");
                }

                await Task.Delay(1, cancellationToken);
            }
        }
    }
}