// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        private void SendResponse(Response response, IPEndPoint remoteEndpoint) {
            if (response.IsResponseNeeded) {
                Log.Debug($"Sending response to {remoteEndpoint}: {response.ResponseMessage}");

                var dataBytes = Encoding.UTF8.GetBytes(response.ResponseMessage);

                try {
                    _listeningSocket.SendTo(dataBytes, dataBytes.Length, SocketFlags.None, remoteEndpoint);
                } catch (SocketException ex) {
                    if (ex.SocketErrorCode == SocketError.HostUnreachable) {
                        Log.Debug(ex, "Can't send local response because host is unreachable, probably subnets don't match. Global response should still go through.");
                    } else if (ex.SocketErrorCode == SocketError.NetworkUnreachable) {
                        Log.Debug(ex, "Can't send local response because network is unreachable, but that is actually ok. Probably NIC doesn't have an IP address yet.");
                    } else {
                        throw;
                    }
                }

                if (response.IsResponseGlobal) {
                    Log.Debug("Sending the same response globally");

                    try {
                        _listeningSocket.SendTo(dataBytes, dataBytes.Length, SocketFlags.None, new IPEndPoint(IPAddress.Broadcast, 50022));
                    } catch (SocketException ex) {
                        if (ex.SocketErrorCode == SocketError.NetworkUnreachable) {
                            Log.Debug(ex, "Can't send global response because network is unreachable, but that is actually ok. Probably NIC doesn't have an IP address yet.");
                        } else if (ex.SocketErrorCode == SocketError.HostUnreachable) {
                            Log.Debug(ex, "Can't send global response because host is unreachable, but that is actually ok. Probably NIC doesn't have an IP address yet.");
                        } else {
                            throw;
                        }
                    }
                }
            } else {
                Log.Debug($"No reply needed for {remoteEndpoint}");
            }
        }
    }
}