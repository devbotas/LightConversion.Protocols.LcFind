// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System.Net;

namespace LightConversion.Protocols.LcFind {
    internal class ClientRawMessage {
        public string Payload { get; set; }
        public IPEndPoint Endpoint { get; set; }
    }
}
