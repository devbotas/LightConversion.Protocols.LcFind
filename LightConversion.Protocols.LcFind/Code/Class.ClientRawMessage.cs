using System.Net;

namespace LightConversion.Protocols.LcFind {
    public class ClientRawMessage {
        public string Payload { get; set; }
        public IPEndPoint Endpoint { get; set; }
    }
}