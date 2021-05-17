// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System.Collections.Generic;
using LightConversion.Protocols.LcFind;
using SimpleMvvmToolkit;

namespace LcFindDeviceDetector {
    public class DeviceDataViewModel : ViewModelBase<DeviceDataViewModel> {
        private DeviceDescription _actualDescription = new();
        private bool _isReachable;
        private string _lookerIpAddress;
        private string _lookerNetworkInterfaceName;
        private string _targetGatewayAddress;
        private string _targetIpAddress;
        private string _targetNetworkMode;
        private string _targetSubnetMask;

        public DeviceDescription ActualDescription {
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
        public bool IsReachable {
            get { return _isReachable; }
            set {
                if (_isReachable == value) return;

                _isReachable = value;
                NotifyPropertyChanged(m => m.IsReachable);
            }
        }
        public bool IsUsingDhcp => ActualDescription.NetworkMode == "DHCP";

        public bool IsUsingStaticIp => ActualDescription.NetworkMode == "Static";

        public string LookerIpAddress {
            get { return _lookerIpAddress; }
            set {
                if (_lookerIpAddress == value) return;

                _lookerIpAddress = value;
                NotifyPropertyChanged(m => m.LookerIpAddress);
            }
        }
        public string LookerNetworkInterfaceName {
            get { return _lookerNetworkInterfaceName; }
            set {
                if (_lookerNetworkInterfaceName == value) return;

                _lookerNetworkInterfaceName = value;
                NotifyPropertyChanged(m => m.LookerNetworkInterfaceName);
            }
        }
        public string TargetGatewayAddress {
            get { return _targetGatewayAddress; }
            set {
                if (_targetGatewayAddress == value) return;

                _targetGatewayAddress = value;
                NotifyPropertyChanged(m => m.TargetGatewayAddress);
            }
        }

        public string TargetIpAddress {
            get { return _targetIpAddress; }
            set {
                if (_targetIpAddress == value) return;

                _targetIpAddress = value;
                NotifyPropertyChanged(m => m.TargetIpAddress);
            }
        }

        public bool TargetIsUsingDhcp => TargetNetworkMode == "DHCP";

        public bool TargetIsUsingStaticIp => TargetNetworkMode == "Static";

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
        public string TargetSubnetMask {
            get { return _targetSubnetMask; }
            set {
                if (_targetSubnetMask == value) return;

                _targetSubnetMask = value;
                NotifyPropertyChanged(m => m.TargetSubnetMask);
            }
        }
    }
}
