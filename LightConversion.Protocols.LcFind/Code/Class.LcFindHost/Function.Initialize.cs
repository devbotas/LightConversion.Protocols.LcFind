// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
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
    }
}