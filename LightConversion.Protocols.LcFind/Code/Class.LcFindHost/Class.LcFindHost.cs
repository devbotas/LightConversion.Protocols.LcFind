// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NLog;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost : IDisposable {
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
    }
}