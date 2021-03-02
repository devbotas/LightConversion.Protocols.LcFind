// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;
using System.Net;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        private void Tick() {
            var responseMessage = "";
            var isConfReqActionNeeded = false;
            NetworkConfiguration receivedConfiguration = null;

            var gotSomething = _udpReceiveQueue.TryDequeue(out var receivedMessage);

            if (gotSomething) {
                if (receivedMessage.Payload.StartsWith("FINDReq=1;")) {
                    if (_tryGetNetworkConfigurationDelegate(out var actualConfig)) {
                        responseMessage = BuildFindReqResponseString(actualConfig);
                        _requestedNewConfigurationEndpoint = receivedMessage.Endpoint;
                    } else {
                        Log.Error("Could not retrieve actual network configuration, and so cannot send a proper response to FINDReq request.");
                    }
                }

                if (receivedMessage.Payload.StartsWith($"CONFReq=1;HWADDR={_hwAddress};")) {
                    var isOk = NetworkConfiguration.TryFromRequestString(receivedMessage.Payload, out receivedConfiguration, out var requestResult);
                    _requestedNewConfigurationEndpoint = receivedMessage.Endpoint;

                    if (isOk) {
                        _requestedNewConfiguration = receivedConfiguration;
                        isConfReqActionNeeded = true;
                    } else {
                        responseMessage = BuildConfReqResponseString(requestResult);
                    }
                }
            }

            if ((ActualStatus == Status.Ready) && (_targetStatus == Status.Ready)) {
                if (IsReconfigurationEnabled && isConfReqActionNeeded) {
                    if (IsConfirmationEnabled) {
                        _targetStatus = Status.AwaitingConfirmation;
                    } else {
                        _targetStatus = Status.Cooldown;
                    }
                }
            } else if ((ActualStatus == Status.Ready) && (_targetStatus == Status.Cooldown)) {
                var requestResult = "";

                Log.Info($"Trying to set new network configuration ({_requestedNewConfiguration.IpAddress} / {_requestedNewConfiguration.SubnetMask}) ...");
                if (_trySetNetworkConfigurationDelegate(_requestedNewConfiguration)) {
                    Log.Info($"New configuration set. Host will now spend {CooldownTimeout} seconds in {nameof(Status.Cooldown)} state.");
                    _cooldownEnd = DateTime.Now.AddSeconds(CooldownTimeout);
                    requestResult = "Ok";
                    ActualStatus = Status.Cooldown;
                } else {
                    Log.Error($"Unable to set requested configuration.");
                    requestResult = "Error-Unable to set requested configuration";
                    ActualStatus = Status.Ready;
                }

                responseMessage = BuildConfReqResponseString(requestResult);
            } else if ((ActualStatus == Status.Ready) && (_targetStatus == Status.AwaitingConfirmation)) {
                // todo.
            } else if ((ActualStatus == Status.Ready) && (_targetStatus == Status.Disabled)) {
                IsReconfigurationEnabled = false;
                ActualStatus = Status.Disabled;
                Log.Info($"Going to state {nameof(Status.Disabled)} upon host's request. LC-FIND is now disabled.");
            } else if ((ActualStatus == Status.AwaitingConfirmation) && (_targetStatus == Status.AwaitingConfirmation)) {
                // todo.
            } else if ((ActualStatus == Status.AwaitingConfirmation) && (_targetStatus == Status.Cooldown)) {
                // todo.
            } else if ((ActualStatus == Status.AwaitingConfirmation) && (_targetStatus == Status.Ready)) {
                // todo.
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Disabled)) {
                IsReconfigurationEnabled = false;
                ActualStatus = Status.Disabled;
                Log.Info($"Going to state {nameof(Status.Disabled)} upon host's request. LC-FIND is now disabled.");
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Cooldown)) {
                if (DateTime.Now >= _cooldownEnd) {
                    _targetStatus = Status.Ready;
                }
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Ready)) {
                Log.Info($"Cooldown period expired, going to state {nameof(Status.Ready)}.");
                ActualStatus = Status.Ready;
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Disabled)) {
                IsReconfigurationEnabled = false;
                ActualStatus = Status.Disabled;
                Log.Info($"Going to state {nameof(Status.Disabled)} upon host's request. LC-FIND is now disabled.");
            } else if ((ActualStatus == Status.Disabled) && (_targetStatus == Status.Ready)) {
                ActualStatus = Status.Ready;
                IsReconfigurationEnabled = true;
                Log.Info($"Going to state {nameof(Status.Ready)} upon host's request. LC-FIND is now enabled.");
            }

            if (responseMessage != "") {
                SendResponse(responseMessage, _requestedNewConfigurationEndpoint);
            }
        }
    }
}