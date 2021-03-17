// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
                DetectedDevices.Add(new DeviceDataViewModel {
                    ActualDescription = new DeviceFinder.DeviceDescription {
                        DeviceName = "Pharos",
                        IpAddress = "192.168.11.251",
                        IsReachable = true,
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
                        var foundDevices = DeviceFinder.LookForDevices();
                        IsSomethingDetected = foundDevices.Count > 0;

                        var deviceViewModels = new List<DeviceDataViewModel>();
                        foreach (var deviceDescription in foundDevices) {
                            deviceViewModels.Add(new DeviceDataViewModel { ActualDescription = deviceDescription });
                        }

                        DetectedDevices = deviceViewModels;
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
                        DeviceFinder.ReconfigureDeviceWithDhcp(parameter.ActualDescription.MacAddress, parameter.ActualDescription.LookerIpAddress);
                    }

                    if (parameter.TargetIsUsingStaticIp) {
                        DeviceFinder.ReconfigureDeviceWithStaticIp(parameter.ActualDescription.MacAddress, parameter.ActualDescription.LookerIpAddress, parameter.TargetIpAddress, parameter.TargetSubnetMask, parameter.TargetGatewayAddress);
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

        public void Dispose() {}
    }
}