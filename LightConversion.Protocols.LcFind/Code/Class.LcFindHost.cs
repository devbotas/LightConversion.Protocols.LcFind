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
        public Status ActualStatus { get; private set; } = Status.Disabled;
        private Status _targetStatus;

        private int _confirmationCounter = 0;
        private DateTime _cooldownEnd = new DateTime(2020, 01, 1);
        public int ConfirmationTimeout { get; set; } = 60;
        public int CooldownTimeout { get; set; } = 60;
        public bool IsConfirmationEnabled { get; set; }
        public bool IsReconfigurationEnabled { get; private set; }
        private NetworkConfiguration _configurationToSet;
        IPEndPoint _remoteEndpoint = new IPEndPoint(0, 0);

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
                    while (_globalCancellationTokenSource.IsCancellationRequested == false) {
                        Tick();
                        await Task.Delay(1);
                    }
                } catch (Exception ex) {
                    Log.Error(ex, $"{nameof(Tick)} task failed with exception. Host will shut down now...");

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

        public void EnableReconfiguration() {
            if (ActualStatus == Status.Disabled) {
                _targetStatus = Status.Ready;
            }
        }

        public void DisableReconfiguration() {
            _targetStatus = Status.Disabled;
        }

        public void Confirm() {
            if (ActualStatus == Status.AwaitingConfirmation) {
                _targetStatus = Status.Cooldown;
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

#warning It is neither null nor empty at this point?
                    if (string.IsNullOrEmpty(receivedMessage) == false) {
                        _remoteEndpoint = remoteEndpoint;
                        var response = ProcessMessage(receivedMessage);
                        SendResponse(response, remoteEndpoint);
                    }
                } else {
                    Log.Warn("Message of zero length received");
                }

                await Task.Delay(1, cancellationToken);
            }
        }

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
                } else {
                    Log.Error("Could not retrieve actual network configuration, and so cannot send a proper response to FINDReq request.");
                }
            }

            if (IsReconfigurationEnabled && receivedMessage.StartsWith($"CONFReq=1;HWADDR={_hwAddress};")) {
                var isOk = NetworkConfiguration.TryFromResponseString(receivedMessage, out var receivedConfiguration, out var requestResult);

                if (isOk) {
                    if (IsNewIpValid(receivedConfiguration.IpAddress) == false) {
                        isOk = false;
                        requestResult = "Cannot use this IP address";
                    }
                }

                if (isOk) {
                    _configurationToSet = receivedConfiguration;

                    if (IsConfirmationEnabled) {
                        _targetStatus = Status.AwaitingConfirmation;
                    } else {
                        _targetStatus = Status.Cooldown;
                    }

                    //if (_trySetNetworkConfigurationDelegate(receivedConfiguration)) {
                    //    _cooldownCounter = CooldownTimeout;
                    //    requestResult = "Ok";
                    //    ActualStatus = Status.Cooldown;
                    //} else {
                    //    requestResult = "Unable to set requested configuration";
                    //    ActualStatus = Status.Ready;
                    //}
                } else {
#warning this global response... Do I have to send it here, on error?
                    response = new Response(true, true, BuildConfReqResponseString(requestResult));
                }

                // response = new Response(true, true, BuildConfReqResponseString(requestResult));
            }

            return response;
        }

        private string BuildConfReqResponseString(string requestResult) {
            var returnString = "";
            var responseBuilder = new StringBuilder();

            responseBuilder.Append("CONF=1;");
#warning _hwaddress should become a property maybe?..
            responseBuilder.Append($"HWADDR={_hwAddress};");
            responseBuilder.Append($"Status={ActualStatus};");
            responseBuilder.Append($"Result={requestResult};");


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

            responseBuilder.Append("\0");

            returnString = responseBuilder.ToString();


            return returnString;
        }

        private void Tick() {
            if ((ActualStatus == Status.Ready) && (_targetStatus == Status.Ready)) {
                // bybis.
            } else if ((ActualStatus == Status.Ready) && (_targetStatus == Status.Disabled)) {
                ActualStatus = Status.Disabled;
            } else if ((ActualStatus == Status.Ready) && (_targetStatus == Status.Cooldown)) {
                var requestResult = "";

                if (_trySetNetworkConfigurationDelegate(_configurationToSet)) {
                    _cooldownEnd = DateTime.Now.AddSeconds(CooldownTimeout);
                    requestResult = "Ok";
                    ActualStatus = Status.Cooldown;
                } else {
                    requestResult = "Unable to set requested configuration";
                    ActualStatus = Status.Ready;
                }

                var responseString = BuildConfReqResponseString(requestResult);
                var response = new Response(true, true, BuildConfReqResponseString(requestResult));
                SendResponse(response, _remoteEndpoint);
                ActualStatus = Status.Cooldown;
            } else if ((ActualStatus == Status.Ready) && (_targetStatus == Status.AwaitingConfirmation)) {
                // todo.
            } else if ((ActualStatus == Status.Ready) && (_targetStatus == Status.Disabled)) {
                IsReconfigurationEnabled = false;
                ActualStatus = Status.Disabled;
            } else if ((ActualStatus == Status.AwaitingConfirmation) && (_targetStatus == Status.AwaitingConfirmation)) {
                // todo.
            } else if ((ActualStatus == Status.AwaitingConfirmation) && (_targetStatus == Status.Cooldown)) {
                // todo.
            } else if ((ActualStatus == Status.AwaitingConfirmation) && (_targetStatus == Status.Ready)) {
                // todo.
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Disabled)) {
                IsReconfigurationEnabled = false;
                ActualStatus = Status.Disabled;
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Cooldown)) {
                if (DateTime.Now >= _cooldownEnd) {
                    _targetStatus = Status.Ready;
                }
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Ready)) {
                ActualStatus = Status.Ready;
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Disabled)) {
                IsReconfigurationEnabled = false;
                ActualStatus = Status.Disabled;
            } else if ((ActualStatus == Status.Disabled) && (_targetStatus == Status.Ready)) {
                ActualStatus = Status.Ready;
                IsReconfigurationEnabled = true;
            }
        }

        private bool SetNewConfiguration() {
            return true;
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
#warning do we still need those bools?
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