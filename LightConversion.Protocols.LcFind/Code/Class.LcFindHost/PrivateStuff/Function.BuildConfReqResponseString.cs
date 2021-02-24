// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System.Text;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        private string BuildConfReqResponseString(string requestResult) {
            var responseBuilder = new StringBuilder();

            responseBuilder.Append("CONF=1;");
            responseBuilder.Append($"HWADDR={_hwAddress};");
            responseBuilder.Append($"Status={ActualStatus};");
            responseBuilder.Append($"Result={requestResult};");

            responseBuilder.Append("\0");

            return responseBuilder.ToString();
        }
    }
}