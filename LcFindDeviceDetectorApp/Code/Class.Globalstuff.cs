// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;

namespace LcFindDeviceDetector {
    public class GlobalStuff : IDisposable {
        public static GlobalStuff Instance { get; } = new GlobalStuff();

        public MirandaViewModel MirandaViewModel { get; } = new MirandaViewModel();

        public void Initialize() {
            MirandaViewModel.Initialize();
        }

        public void Dispose() {
            MirandaViewModel.Dispose();
        }
    }
}
