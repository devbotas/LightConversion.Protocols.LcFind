// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace LightConversion.Protocols.LcFind {
    public class LcFindHost : IDisposable {
        private static readonly Logger Log = LogManager.GetLogger(nameof(LcFindHost));
        private readonly CancellationTokenSource _globalCancellationTokenSource = new CancellationTokenSource();

        private TrySetNetworkConfigurationDelegate _trySetNetworkConfigurationDelegate = null;
        private TryGetNetworkConfigurationDelegate _tryGetNetworkConfigurationDelegate = null;

        public delegate bool TrySetNetworkConfigurationDelegate(NetworkConfiguration newConfiguration);

        public delegate bool TryGetNetworkConfigurationDelegate(out NetworkConfiguration actualConfiguration);

        private string _hwAddress;
        private Socket _listeningSocket;

        public string SerialNumber { get; set; } = $"Unknown-{Guid.NewGuid()}";
        public string DeviceName { get; set; } = $"Unknown-{Guid.NewGuid()}";
        public Status ActualStatus { get; private set; } = Status.Ready;

        private int _confirmationCounter = 0;
        private int _cooldownCounter = 0;
        public int ConfirmationTimeout { get; set; } = 60;
        public int CooldownTimeout { get; set; } = 60;

        public void Initialize(TrySetNetworkConfigurationDelegate trySetNetworkConfigurationDelegate, TryGetNetworkConfigurationDelegate tryGetNetworkConfigurationDelegate) {
            _trySetNetworkConfigurationDelegate = trySetNetworkConfigurationDelegate;
            _tryGetNetworkConfigurationDelegate = tryGetNetworkConfigurationDelegate;

            _listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _listeningSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listeningSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

            try {
                _listeningSocket.Bind(new IPEndPoint(IPAddress.Any, 50022));
            } catch (SocketException ex) {
                Log.Error(ex, "Can't bind to port 50022. Make sure no other program is using it, also wihtout SocketOptionName.ReuseAddress.");

                // No point of continuing, so throwing hardly...
                throw;
            }

            Task.Run(async () => {
                try {
                    await ListenForUdpTrafficContinuouslyAsync(_globalCancellationTokenSource.Token);
                } catch (Exception ex) {
                    Log.Error(ex, $"{nameof(ListenForUdpTrafficContinuouslyAsync)} failed with exception. Host will shut down now...");

                    // No point in continuing...
                    _globalCancellationTokenSource.Cancel();
                }
            });
            Task.Run(async () => {
                try {
                    await MonitorTimeoutCountersContiuouslyAsync(_globalCancellationTokenSource.Token);
                } catch (Exception ex) {
                    Log.Error(ex, $"{nameof(MonitorTimeoutCountersContiuouslyAsync)} task failed with exception. Host will shut down now...");

                    // No point in continuing...
                    _globalCancellationTokenSource.Cancel();
                }
            });

            _tryGetNetworkConfigurationDelegate(out var config);
            _hwAddress = config.MacAddress;
        }

        public void Dispose() {
            _globalCancellationTokenSource.Cancel();
            _listeningSocket.Dispose();
        }

        private async Task MonitorTimeoutCountersContiuouslyAsync(CancellationToken cancellationToken) {
            while (cancellationToken.IsCancellationRequested == false) {
                if (_confirmationCounter > 0) {
                    _confirmationCounter--;
                } else {
                    ActualStatus = Status.Ready;
                }

                if (_cooldownCounter > 0) {
                    _cooldownCounter--;
                } else {
                    ActualStatus = Status.Ready;
                }

                await Task.Delay(1000, cancellationToken);
            }
        }

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

                    if (string.IsNullOrEmpty(receivedMessage) == false) {
                        var response = ProcessMessage(receivedMessage);

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
                } else {
                    Log.Warn("Message of zero length received");
                }

                await Task.Delay(1, cancellationToken);
            }
        }

        private Response ProcessMessage(string receivedMessage) {
            var response = new Response(false, false, "");
            var responseBuilder = new StringBuilder();
            if (receivedMessage.StartsWith("FINDReq=1;")) {
                if (_tryGetNetworkConfigurationDelegate(out var actualConfig)) {
                    responseBuilder.Append("FIND=1;");
                    responseBuilder.Append($"IP={actualConfig.IpAddress};");
                    responseBuilder.Append($"HWADDR={actualConfig.MacAddress};");
                    responseBuilder.Append($"DeviceName={DeviceName};");
                    responseBuilder.Append($"SN={SerialNumber};");
                    responseBuilder.Append($"Status={ActualStatus};");

                    if (actualConfig.IsDhcpEnabled) {
                        responseBuilder.Append("NetworkMode=DHCP;");
                    } else {
                        responseBuilder.Append("NetworkMode=Static;");
                    }

                    responseBuilder.Append($"Mask={actualConfig.SubnetMask};");
                    responseBuilder.Append($"Gateway={actualConfig.GatewayAddress};");
                    responseBuilder.Append("\0");

                    response = new Response(true, true, responseBuilder.ToString());
                }
            } else if (receivedMessage.StartsWith($"CONFReq=1;HWADDR={_hwAddress};")) {
                var isOk = NetworkConfiguration.TryFromResponseString(receivedMessage, out var receivedConfiguration, out var requestResult);

                if (isOk) {
                    if (IsNewIpValid(receivedConfiguration.IpAddress) == false) {
                        isOk = false;
                        requestResult = "Cannot use this IP address";
                    }
                }

                if (isOk) {
                    if (_trySetNetworkConfigurationDelegate(receivedConfiguration)) {
                        _cooldownCounter = CooldownTimeout;
                        requestResult = "Ok";
                        ActualStatus = Status.Cooldown;
                    } else {
                        requestResult = "Unable to set requested configuration";
                        ActualStatus = Status.Ready;
                    }
                }

                responseBuilder.Append("CONF=1;");
#warning _hwaddress should become a property maybe?..
                responseBuilder.Append($"HWADDR={_hwAddress};");
                responseBuilder.Append($"Status={ActualStatus};");
                responseBuilder.Append($"Result={requestResult};");

                if (isOk) {
#warning What is the point in getting configuration, when, at this point, settings reported OK?
                    if (_tryGetNetworkConfigurationDelegate(out var actualConfig)) {
                        if (actualConfig.IsDhcpEnabled) {
                            responseBuilder.Append("NetworkMode=DHCP;");
#warning Shall we not include IP addresses and stuff here?..
                        } else {
                            responseBuilder.Append("NetworkMode=Static;");
                            responseBuilder.Append($"IP={actualConfig.IpAddress};");
                            responseBuilder.Append($"Mask={actualConfig.SubnetMask};");
                            responseBuilder.Append($"Gateway={actualConfig.GatewayAddress};");
                        }
                    }
                }

                responseBuilder.Append("\0");

                response = new Response(true, true, responseBuilder.ToString());
            }

            return response;
        }

        private bool IsNewIpValid(IPAddress newIpAddress) {
            var newIpBytes = newIpAddress.GetAddressBytes();
            var noEmptyAddress = newIpBytes[0] != 0 || newIpBytes[1] != 0 || newIpBytes[2] != 0 || newIpBytes[3] != 0;
            var noLoopback = newIpBytes[0] != 127;
            var noLinkLocal = newIpBytes[0] != 169 || newIpBytes[1] != 254;
            var noTestNet1 = newIpBytes[0] != 192 || newIpBytes[1] != 0;
            var noIpv6Relay = newIpBytes[0] != 192 || newIpBytes[1] != 88 || newIpBytes[2] != 99;
            var noTestNet2 = newIpBytes[0] != 198;
            var noTestNet3 = newIpBytes[0] != 203;
            var noMulticast = newIpBytes[0] != 224;
            var noReserved = newIpBytes[0] != 240;
            var noBroadcast = newIpBytes[0] != 255 || newIpBytes[1] != 255 || newIpBytes[2] != 255 || newIpBytes[3] != 255;

            return noEmptyAddress && noLoopback && noLinkLocal && noTestNet1 && noIpv6Relay && noTestNet2 && noTestNet3 && noMulticast && noReserved && noBroadcast;
        }

        private class Response {
            public readonly bool IsResponseGlobal;
            public readonly bool IsResponseNeeded;
            public readonly string ResponseMessage;

            public Response(bool isResponseNeeded, bool isResponseGlobal, string responseMessage) {
                IsResponseNeeded = isResponseNeeded;
                IsResponseGlobal = isResponseGlobal;
                ResponseMessage = responseMessage;
            }
        }
    }
}