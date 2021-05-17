// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System.ComponentModel;
using System.Windows;

namespace LcFindDeviceDetector {
    public partial class MainWindow {
        public MainWindow() {
            InitializeComponent();
        }

        private void HandleLoadedEvent(object sender, RoutedEventArgs e) {
            GlobalStuff.Instance.Initialize();

            if (GlobalStuff.Instance.MirandaViewModel.ScanCommand.CanExecute(null)) {
                GlobalStuff.Instance.MirandaViewModel.ScanCommand.Execute(null);
            }
        }

        private void HandleClosingEvent(object sender, CancelEventArgs e) {
            GlobalStuff.Instance.Dispose();
        }
    }
}