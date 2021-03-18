// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LightConversion.Protocols.LcFind;
using SimpleMvvmToolkit;

namespace TestClient {
    public class MirandaViewModel : ViewModelBase<MirandaViewModel>, IDisposable {
        private List<DeviceDataViewModel> _detectedDevices = new List<DeviceDataViewModel>();
        public List<DeviceDataViewModel> DetectedDevices {
            get { return _detectedDevices; }
            set {
                if (_detectedDevices == value) return;

                _detectedDevices = value;
                NotifyPropertyChanged(m => m.DetectedDevices);
            }
        }

        private DeviceDataViewModel _selectedDevice = new DeviceDataViewModel();
        public DeviceDataViewModel SelectedDevice {
            get { return _selectedDevice; }
            set {
                if (_selectedDevice == value) return;

                _selectedDevice = value;
                NotifyPropertyChanged(m => m.SelectedDevice);
            }
        }

        private bool _isSomethingDetected;
        public bool IsSomethingDetected {
            get { return _isSomethingDetected; }
            set {
                if (_isSomethingDetected == value) return;

                _isSomethingDetected = value;
                NotifyPropertyChanged(m => m.IsSomethingDetected);
            }
        }

        public MirandaViewModel() {
            if (this.IsInDesignMode()) {
                IsSomethingDetected = true;

                DetectedDevices.Add(new DeviceDataViewModel {
                    ActualDescription = new DeviceDescription {
                        DeviceName = "Pharos",
                        IpAddress = "192.168.11.251",
                        MacAddress = "11:22:33:44:55:66",
                        NetworkMode = "StaticIp",
                        SerialNumber = "PH123456",
                        SubnetMask = "255.255.255.0",
                        GatewayAddress = "192.168.1.1"
                    }
                });
            }
        }

        private bool _isScanCommandBusy;
        public bool IsScanCommandBusy {
            get { return _isScanCommandBusy; }
            set {
                if (_isScanCommandBusy == value) return;

                _isScanCommandBusy = value;
                NotifyPropertyChanged(m => m.IsScanCommandBusy);
            }
        }

        private DelegateCommand _scanCommand;
        public DelegateCommand ScanCommand {
            get {
                if (_scanCommand != null) return _scanCommand;

                _scanCommand = new DelegateCommand(async () => {
                    IsScanCommandBusy = true;
                    DetectedDevices = new List<DeviceDataViewModel>();
                    IsSomethingDetected = false;

                    await Task.Run(() => {
                        var foundDeviceDataViewModels = new List<DeviceDataViewModel>();

                        var localIpAddresses = GetAllLocalIpAddresses();

                        foreach (var localIpAddress in localIpAddresses) {
                            var devicesFoundOnThisInterface = LcFindClient.LookForDevices(localIpAddress.IpAddress);

                            foreach (var deviceDescription in devicesFoundOnThisInterface) {
                                var isReachable = false;
                                using (var ping = new Ping()) {
                                    try {
                                        var pingResult = ping.Send(deviceDescription.IpAddress, 100);
                                        if ((pingResult != null) && (pingResult.Status == IPStatus.Success)) {
                                            isReachable = true;
                                        }
                                    } catch (PingException ex) {
                                        Debug.Print(ex.ToString());
                                    }
                                }

                                foundDeviceDataViewModels.Add(new DeviceDataViewModel { ActualDescription = deviceDescription, IsReachable = isReachable, LookerIpAddress = localIpAddress.IpAddress.ToString(), LookerNetworkInterfaceName = localIpAddress.NetworkInterface });
                            }
                        }

                        IsSomethingDetected = foundDeviceDataViewModels.Count > 0;

                        DetectedDevices = foundDeviceDataViewModels;
                        SelectedDevice = DetectedDevices.FirstOrDefault();
                    });

                    IsScanCommandBusy = false;
                }, () => IsScanCommandBusy == false);

                PropertyChanged += (sender, e) => {
                    switch (e.PropertyName) {
                        case nameof(IsScanCommandBusy):
                            ScanCommand.RaiseCanExecuteChanged();
                            break;
                    }
                };
                return _scanCommand;
            }
        }

        private bool _isSaveCommandBusy;
        public bool IsSaveCommandBusy {
            get { return _isSaveCommandBusy; }
            set {
                if (_isSaveCommandBusy == value) return;

                _isSaveCommandBusy = value;
                NotifyPropertyChanged(m => m.IsSaveCommandBusy);
            }
        }

        private DelegateCommand<DeviceDataViewModel> _saveCommand;
        public DelegateCommand<DeviceDataViewModel> SaveCommand {
            get {
                if (_saveCommand != null) return _saveCommand;

                _saveCommand = new DelegateCommand<DeviceDataViewModel>(async parameter => {
                    IsSaveCommandBusy = true;

                    if (parameter.TargetIsUsingDhcp) {
                        ReconfigureDeviceWithDhcp(parameter.ActualDescription.MacAddress, parameter.LookerIpAddress);
                    }

                    if (parameter.TargetIsUsingStaticIp) {
                        ReconfigureDeviceWithStaticIp(parameter.ActualDescription.MacAddress, parameter.LookerIpAddress, parameter.TargetIpAddress, parameter.TargetSubnetMask, parameter.TargetGatewayAddress);
                    }

                    await Task.Delay(10000);

                    IsSaveCommandBusy = false;

                    if (ScanCommand.CanExecute(null)) ScanCommand.Execute(null);
                }, parameter => IsSaveCommandBusy == false);

                PropertyChanged += (sender, e) => {
                    switch (e.PropertyName) {
                        case nameof(IsSaveCommandBusy):
                            SaveCommand.RaiseCanExecuteChanged();
                            break;
                    }
                };
                return _saveCommand;
            }
        }

        public void Initialize() {}

        public void Dispose() {
#warning Nice implementation!
        }

        public static List<(string NetworkInterface, IPAddress IpAddress)> GetAllLocalIpAddresses() {
            var localIpAddresses = new List<(string NetworkInterface, IPAddress IpAddress)>();

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces()) {
                var isValidInterface = networkInterface.OperationalStatus == OperationalStatus.Up;
                isValidInterface &= networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback;

                if (isValidInterface) {
                    var ipProperties = networkInterface.GetIPProperties();

                    var relevantUnicastIpAddress = ipProperties.UnicastAddresses.FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);

                    if (relevantUnicastIpAddress != null) {
                        localIpAddresses.Add((networkInterface.Name, relevantUnicastIpAddress.Address));
                    }
                } else {
                    Debug.Print($"Skipping interface \"{networkInterface.Name}\".");
                }
            }

            return localIpAddresses;
        }

        public static void ReconfigureDeviceWithStaticIp(string actualMacAddress, string localAdapterAddress, string newIpAddress, string newSubnetMask, string newGatewayAddress) {
            try {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)) {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

                    socket.Bind(new IPEndPoint(IPAddress.Parse(localAdapterAddress), 50022));

                    var messageToSend = Encoding.UTF8.GetBytes($"CONFReq=1;HWADDR={actualMacAddress};NetworkMode=Static;IP={newIpAddress};Mask={newSubnetMask};Gateway={newGatewayAddress}\0");
                    socket.SendTo(messageToSend, new IPEndPoint(IPAddress.Broadcast, 50022));
                }
            } catch (SocketException ex) {
                // TODO: logging.
                Debug.Print(ex.ToString());
            }
        }

        public static void ReconfigureDeviceWithDhcp(string actualMacAddress, string localAdapterAddress) {
            try {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)) {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

                    socket.Bind(new IPEndPoint(IPAddress.Parse(localAdapterAddress), 50022));

                    var messageToSend = Encoding.UTF8.GetBytes($"CONFReq=1;HWADDR={actualMacAddress};NetworkMode=DHCP\0");
                    socket.SendTo(messageToSend, new IPEndPoint(IPAddress.Broadcast, 50022));
                }
            } catch (SocketException ex) {
                // TODO: logging.
                Debug.Print(ex.ToString());
            }
        }
    }
}