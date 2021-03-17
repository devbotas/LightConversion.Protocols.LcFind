﻿// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System.Collections.Generic;
using SimpleMvvmToolkit;

namespace TestClient {
    public class DeviceDataViewModel : ViewModelBase<DeviceDataViewModel> {
        private DeviceFinder.DeviceDescription _actualDescription = new DeviceFinder.DeviceDescription();
        public DeviceFinder.DeviceDescription ActualDescription {
            get { return _actualDescription; }
            set {
                if (_actualDescription == value) return;

                _actualDescription = value;

                NotifyPropertyChanged(m => m.ActualDescription);
                NotifyPropertyChanged(m => m.IsUsingDhcp);
                NotifyPropertyChanged(m => m.IsUsingStaticIp);

                TargetIpAddress = _actualDescription.IpAddress;
                TargetGatewayAddress = _actualDescription.GatewayAddress;
                TargetSubnetMask = _actualDescription.SubnetMask;
                TargetNetworkMode = _actualDescription.NetworkMode;
            }
        }

        public List<string> AvailableNetworkModes { get; } = new List<string> { "DHCP", "Static" };

        private string _targetNetworkMode;
        public string TargetNetworkMode {
            get { return _targetNetworkMode; }
            set {
                if (_targetNetworkMode == value) return;

                _targetNetworkMode = value;
                NotifyPropertyChanged(m => m.TargetNetworkMode);
                NotifyPropertyChanged(m => m.TargetIsUsingDhcp);
                NotifyPropertyChanged(m => m.TargetIsUsingStaticIp);
            }
        }

        private string _targetIpAddress;
        public string TargetIpAddress {
            get { return _targetIpAddress; }
            set {
                if (_targetIpAddress == value) return;

                _targetIpAddress = value;
                NotifyPropertyChanged(m => m.TargetIpAddress);
            }
        }

        private string _targetSubnetMask;
        public string TargetSubnetMask {
            get { return _targetSubnetMask; }
            set {
                if (_targetSubnetMask == value) return;

                _targetSubnetMask = value;
                NotifyPropertyChanged(m => m.TargetSubnetMask);
            }
        }

        private string _targetGatewayAddress;
        public string TargetGatewayAddress {
            get { return _targetGatewayAddress; }
            set {
                if (_targetGatewayAddress == value) return;

                _targetGatewayAddress = value;
                NotifyPropertyChanged(m => m.TargetGatewayAddress);
            }
        }

        public bool IsUsingDhcp => ActualDescription.NetworkMode == "DHCP";
        public bool IsUsingStaticIp => ActualDescription.NetworkMode == "Static";

        public bool TargetIsUsingDhcp => TargetNetworkMode == "DHCP";
        public bool TargetIsUsingStaticIp => TargetNetworkMode == "Static";
    }
}