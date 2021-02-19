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
                var requestResult = "";

                var isOk = NetworkConfiguration.TryFromResponseString(receivedMessage, out var receivedConfiguration);

                if (isOk) {
                    if (_trySetNetworkConfigurationDelegate(receivedConfiguration)) {
                        requestResult = "Ok";
                        _cooldownCounter = CooldownTimeout;
                        ActualStatus = Status.Cooldown;
                    } else {
                        requestResult = "Error";
                        ActualStatus = Status.Ready;
                    }
                } else {
                    requestResult = "InvalidData";
                    ActualStatus = Status.Ready;
                }

                responseBuilder.Append("CONF=1;");
                responseBuilder.Append($"HWADDR={_hwAddress};");
                responseBuilder.Append($"Status={ActualStatus};");
                if (requestResult == "Ok") {
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

        private static string GetParameterFromSeggerString(string seggerString, string parameterName) {
            var parts = seggerString.Split(';');

            for (var i = 0; i < parts.Length; i++) {
                if (parts[i].StartsWith($"{parameterName}=")) {
                    return parts[i].Substring(parameterName.Length + 1).Trim('\0', ' ', '\r', '\n');
                }
            }

            return "";
        }

        private bool IsNewIpValid(string newIpAddressString) {
            var isNewIpGood = IPAddress.TryParse(newIpAddressString, out var newIpAddress);
            if (isNewIpGood) {
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
            } else {
                return false;
            }
        }

        private bool IsNewMaskValid(string newSubnetMaskString) {
            var isNewMaskGood = IPAddress.TryParse(newSubnetMaskString, out var newSubnetMask);
            if (isNewMaskGood) {
                var newMaskBytes = newSubnetMask.GetAddressBytes();
                Array.Reverse(newMaskBytes);
                var newMask = BitConverter.ToUInt32(newMaskBytes, 0);
                // Shifting the mask to check if there are no set bits after the first unset bit.
                while ((newMask & 0x80000000) == 0x80000000) {
                    newMask <<= 1;
                }

                if (newMask == 0) {
                    return true;
                } else {
                    return false;
                }
            } else {
                return false;
            }
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