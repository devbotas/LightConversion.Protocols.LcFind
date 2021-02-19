// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;
using System.Threading;
using LightConversion.Protocols.LcFind;

namespace TestClient {
    class Program {
        static void Main(string[] args) {
            Console.WriteLine("Spinning up device finder.");

            while (true) {
                var foundDevices = LcFindClient.LookForDevices();

                foreach (var deviceDescription in foundDevices) {
                    Console.WriteLine($"Found: {deviceDescription.DeviceName} at {deviceDescription.IpAddress}");
                }

                Thread.Sleep(5000);
            }
        }
    }
}