// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        private void SendResponse(string responseMessage, IPEndPoint remoteEndpoint) {
            var dataBytes = Encoding.UTF8.GetBytes(responseMessage);

            // This response is to conform to Segger FIND protocol. It only accepts responses directly back to the sender.
            // If host and client are on the different subnets, response will not be received, though.
            _log.Debug($"Sending response to {remoteEndpoint}: {responseMessage}");
            try {
                _listeningSocket.SendTo(dataBytes, dataBytes.Length, SocketFlags.None, remoteEndpoint);
            } catch (SocketException ex) {
                if (ex.SocketErrorCode == SocketError.HostUnreachable) {
                    _log.Debug(ex, "Can't send local response because host is unreachable, probably subnets don't match. Global response should still go through.");
                } else if (ex.SocketErrorCode == SocketError.NetworkUnreachable) {
                    _log.Debug(ex, "Can't send local response because network is unreachable, but that is actually ok. Probably NIC doesn't have an IP address yet.");
                } else {
                    throw;
                }
            }

            // Also broadcasting the same response, because this way it may pass through different subnets and stuff. 
            _log.Debug("Sending the same response globally");
            try {
                _listeningSocket.SendTo(dataBytes, dataBytes.Length, SocketFlags.None, new IPEndPoint(IPAddress.Broadcast, 50022));
            } catch (SocketException ex) {
                if (ex.SocketErrorCode == SocketError.NetworkUnreachable) {
                    _log.Debug(ex, "Can't send global response because network is unreachable, but that is actually ok. Probably NIC doesn't have an IP address yet.");
                } else if (ex.SocketErrorCode == SocketError.HostUnreachable) {
                    _log.Debug(ex, "Can't send global response because host is unreachable, but that is actually ok. Probably NIC doesn't have an IP address yet.");
                } else {
                    throw;
                }
            }
        }
    }
}
