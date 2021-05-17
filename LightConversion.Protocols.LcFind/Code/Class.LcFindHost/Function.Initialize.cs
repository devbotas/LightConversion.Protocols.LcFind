// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        /// <summary>
        /// Initializes a <see cref="LcFindHost"/> instance. 
        /// </summary>
        /// <param name="trySetNetworkConfigurationDelegate">A delegate function which actually sets the network configuration. It is widely platform dependent. If they work on your system, you may use methods from <see cref="LinuxNetworkHelpers"/> and <see cref="WindowsNetworkHelpers"/>, otherwise you may have to implement your own.</param>
        /// <param name="tryGetNetworkConfigurationDelegate">A delegate function which actually reads the network configuration. It is widely platform dependent. If they work on your system, you may use methods from <see cref="LinuxNetworkHelpers"/> and <see cref="WindowsNetworkHelpers"/>, otherwise you may have to implement your own.</param>
        public void Initialize(TrySetNetworkConfigurationDelegate trySetNetworkConfigurationDelegate, TryGetNetworkConfigurationDelegate tryGetNetworkConfigurationDelegate) {
            _trySetNetworkConfigurationDelegate = trySetNetworkConfigurationDelegate;
            _tryGetNetworkConfigurationDelegate = tryGetNetworkConfigurationDelegate;

            _listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _listeningSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listeningSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

            try {
                _listeningSocket.Bind(new IPEndPoint(IPAddress.Any, 50022));
            } catch (SocketException ex) {
                _log.Error(ex, "Can't bind to port 50022. Make sure no other program is using it, also without SocketOptionName.ReuseAddress.");

                // No point of continuing, so throwing hardly...
                throw;
            }

            Task.Run(async () => {
                try {
                    while (_globalCancellationTokenSource.IsCancellationRequested == false) {
                        var gotSomething = TryReadUdpTraffic(out var payload, out var endpoint);

                        if (gotSomething) {
                            _udpReceiveQueue.Enqueue(new ClientRawMessage { Payload = payload, Endpoint = endpoint });
                            if (_udpReceiveQueue.Count > 10) {
                                _udpReceiveQueue.TryDequeue(out _);
                                _log.Warn("UDP input queue is full, discarding an element.");
                            }
                        }

                        await Task.Delay(1);
                    }
                } catch (Exception ex) {
                    _log.Error(ex, $"UDP receiving task failed with exception. Host will shut down now...");

                    // No point in continuing...
                    _globalCancellationTokenSource.Cancel();
                }
            });

            Task.Run(async () => {
                try {
                    while (_globalCancellationTokenSource.IsCancellationRequested == false) {
                        if (_udpSendQueue.TryDequeue(out var response)) {
                            SendResponse(response.Payload, response.Endpoint);
                        }

                        await Task.Delay(100);
                    }
                } catch (Exception ex) {
                    _log.Error(ex, $"UDP sending task failed with exception. Host will shut down now...");

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
                    _log.Error(ex, $"{nameof(Tick)} task failed with exception. Host will shut down now...");

                    // No point in continuing...
                    _globalCancellationTokenSource.Cancel();
                }
            });

            _tryGetNetworkConfigurationDelegate(out var config);
            _hwAddress = config.MacAddress;

            _isInitialized = true;
        }
    }
}
