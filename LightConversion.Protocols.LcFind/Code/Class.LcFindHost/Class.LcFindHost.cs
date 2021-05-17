// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.


using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using NLog;

namespace LightConversion.Protocols.LcFind {

    public partial class LcFindHost : IDisposable {
        private static readonly Logger Log = LogManager.GetLogger(nameof(LcFindHost));
        private readonly CancellationTokenSource _globalCancellationTokenSource = new();
        private bool _isInitialized;
        private readonly ConcurrentQueue<ClientRawMessage> _udpReceiveQueue = new();
        private readonly ConcurrentQueue<ClientRawMessage> _udpSendQueue = new();
        private DateTime _confirmationEnd = new(2020, 01, 1);
        private DateTime _cooldownEnd = new(2020, 01, 1);
        private string _hwAddress;
        private Socket _listeningSocket;
        private Status _targetStatus;
        private TryGetNetworkConfigurationDelegate _tryGetNetworkConfigurationDelegate;
        private TrySetNetworkConfigurationDelegate _trySetNetworkConfigurationDelegate;
        private ClientRawMessage _unansweredConfRequest = null;

        public delegate bool TryGetNetworkConfigurationDelegate(out NetworkConfiguration actualConfiguration);
        public delegate bool TrySetNetworkConfigurationDelegate(NetworkConfiguration newConfiguration);

        /// <summary>
        /// Actual state of the LC-FIND host service.
        /// </summary>
        public Status ActualStatus { get; private set; } = Status.Disabled;

        /// <summary>
        /// If confirmation is enabled, wait for this many seconds before cancelling IP change request and returning to <see cref="Status.Ready"/> state.
        /// </summary>
        public int ConfirmationTimeout { get; set; } = 60;

        /// <summary>
        /// After setting a new configuration, host will wait for this many seconds before accepting new configuration change requests (host will respond to FINDReq messages normally). 
        /// </summary>
        public int CooldownTimeout { get; set; } = 60;

        /// <summary>
        /// Device name to return in a FIND response.
        /// </summary>
        public string DeviceName { get; set; } = $"Unknown-{Guid.NewGuid()}";

        /// <summary>
        /// Enables or disables manual confirmation of configuration change. Usually there's a physical button for the user to press.
        /// </summary>
        public bool IsConfirmationEnabled { get; set; }

        /// <summary>
        /// Shows if reconfiguration is enabled on the device. FINDReq messages will still be processed even if <see cref="IsReconfigurationEnabled"/> is set to false.
        /// </summary>
        public bool IsReconfigurationEnabled { get; private set; }

        /// <summary>
        /// Serial number of the device to return in FIND response.
        /// </summary>
        public string SerialNumber { get; set; } = $"Unknown-{Guid.NewGuid()}";
    }
}
