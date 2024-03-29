﻿// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        /// <summary>
        /// That's the main state machine. It is ticked periodically, thus, all the requests are also handle on a periodic basis (not event-based).
        /// </summary>
        private void Tick() {
            var responseMessage = "";

            // Let's see if there are requests of any kind in the receive queue.
            var gotSomething = _udpReceiveQueue.TryDequeue(out var receivedMessage);
            if (gotSomething) {
                // FINDReq are easy, handling them immediatelly.
                if (receivedMessage.Payload.StartsWith("FINDReq=1;")) {
                    if (_tryGetNetworkConfigurationDelegate(out var actualConfig)) {
                        responseMessage = BuildFindReqResponseString(actualConfig);
                        _udpSendQueue.Enqueue(new ClientRawMessage { Payload = responseMessage, Endpoint = receivedMessage.Endpoint });
                    } else {
                        _log.Error("Could not retrieve actual network configuration, and so cannot send a proper response to FINDReq request.");
                    }
                }

                // CONFReq is more complicated, because I cannot handle them in one tick. Thus, caching the request which will be processed in a couple of ticks.
                if (receivedMessage.Payload.StartsWith($"CONFReq=1;HWADDR={_hwAddress};")) {
                    var isOk = NetworkConfiguration.TryFromRequestString(receivedMessage.Payload, out _, out var requestResult);

                    if (isOk) {
                        _unansweredConfRequest = receivedMessage;
                    } else {
                        _log.Debug($"Client sent a malformed CONFReq message. Result: {requestResult}.");
                        responseMessage = BuildConfReqResponseString(requestResult);
                        _udpSendQueue.Enqueue(new ClientRawMessage { Payload = responseMessage, Endpoint = receivedMessage.Endpoint });
                        isOk = false;
                    }

                    if (isOk) {
                        if (ActualStatus == Status.Cooldown) {
                            _log.Info($"Host is in cooldown, so rejecting a CONFReq message.");
                            responseMessage = BuildConfReqResponseString("Error-Host is in cooldown");
                            _udpSendQueue.Enqueue(new ClientRawMessage { Payload = responseMessage, Endpoint = receivedMessage.Endpoint });
                            _unansweredConfRequest = null;
                        }
                        if (ActualStatus == Status.AwaitingConfirmation) {
                            _log.Info($"Host is already awaiting confirmation, so rejecting a CONFReq message.");
                            responseMessage = BuildConfReqResponseString("Error-Host is already awaiting confirmation");
                            _udpSendQueue.Enqueue(new ClientRawMessage { Payload = responseMessage, Endpoint = receivedMessage.Endpoint });
                            _unansweredConfRequest = null;
                        }
                    }
                }
            }

            // Iterating over all possible states and transitions.
            if ((ActualStatus == Status.Ready) && (_targetStatus == Status.Ready)) {
                if (IsReconfigurationEnabled && (_unansweredConfRequest != null)) {
                    if (IsConfirmationEnabled) {
                        _log.Info($"Client at {_unansweredConfRequest.Endpoint.Address} requested a configuration change, but host requires confirmation, so waiting for it.");
                        _targetStatus = Status.AwaitingConfirmation;
                    } else {
                        _log.Info($"Client at {_unansweredConfRequest.Endpoint.Address} requested to change configuration. Host confirmation is disabled, so proceeding.");
                        _targetStatus = Status.Cooldown;
                    }
                }
            } else if ((ActualStatus == Status.Ready) && (_targetStatus == Status.Cooldown)) {
                NetworkConfiguration.TryFromRequestString(_unansweredConfRequest.Payload, out var requestedNewConfiguration, out var requestResult);

                _log.Info($"Trying to set new network configuration ({requestedNewConfiguration.IpAddress} / {requestedNewConfiguration.SubnetMask} / {requestedNewConfiguration.GatewayAddress} / {requestedNewConfiguration.IsDhcpEnabled}) ...");
                if (_trySetNetworkConfigurationDelegate(requestedNewConfiguration)) {
                    _log.Info($"New configuration set. Host will now spend {CooldownTimeout} seconds in {nameof(Status.Cooldown)} state.");
                    _cooldownEnd = DateTime.Now.AddSeconds(CooldownTimeout);
                    requestResult = "Ok";
                    ActualStatus = Status.Cooldown;
                } else {
                    _log.Error($"Unable to set requested configuration.");
                    requestResult = "Error-Unable to set requested configuration";
                    ActualStatus = Status.Ready;
                    _targetStatus = Status.Ready;
                }

                responseMessage = BuildConfReqResponseString(requestResult);
                _udpSendQueue.Enqueue(new ClientRawMessage { Payload = responseMessage, Endpoint = _unansweredConfRequest.Endpoint });
                _unansweredConfRequest = null;
            } else if ((ActualStatus == Status.Ready) && (_targetStatus == Status.AwaitingConfirmation)) {
                ActualStatus = Status.AwaitingConfirmation;
                _log.Info($"Going to state {nameof(Status.AwaitingConfirmation)} because host requires manual confirmation of the configuration change. Confirmation timeout is {ConfirmationTimeout} seconds.");
                _confirmationEnd = DateTime.Now.AddSeconds(ConfirmationTimeout);
            } else if ((ActualStatus == Status.Ready) && (_targetStatus == Status.Disabled)) {
                IsReconfigurationEnabled = false;
                ActualStatus = Status.Disabled;
                _log.Info($"Going to state {nameof(Status.Disabled)} upon host's request. LC-FIND is now disabled.");
            } else if ((ActualStatus == Status.AwaitingConfirmation) && (_targetStatus == Status.AwaitingConfirmation)) {
                if (DateTime.Now >= _confirmationEnd) {
                    _targetStatus = Status.Ready;
                }
            } else if ((ActualStatus == Status.AwaitingConfirmation) && (_targetStatus == Status.Cooldown)) {
                _log.Info($"Host confirmed configuration change, applying.");

                NetworkConfiguration.TryFromRequestString(_unansweredConfRequest.Payload, out var requestedNewConfiguration, out var requestResult);

                _log.Info($"Trying to set new network configuration ({requestedNewConfiguration.IpAddress} / {requestedNewConfiguration.SubnetMask} / {requestedNewConfiguration.GatewayAddress} / {requestedNewConfiguration.IsDhcpEnabled}) ...");
                if (_trySetNetworkConfigurationDelegate(requestedNewConfiguration)) {
                    _log.Info($"New configuration set. Host will now spend {CooldownTimeout} seconds in {nameof(Status.Cooldown)} state.");
                    _cooldownEnd = DateTime.Now.AddSeconds(CooldownTimeout);
                    requestResult = "Ok";
                    ActualStatus = Status.Cooldown;
                } else {
                    _log.Error($"Unable to set requested configuration.");
                    requestResult = "Error-Unable to set requested configuration";
                    ActualStatus = Status.Ready;
                }

                responseMessage = BuildConfReqResponseString(requestResult);
                _udpSendQueue.Enqueue(new ClientRawMessage { Payload = responseMessage, Endpoint = _unansweredConfRequest.Endpoint });
                _unansweredConfRequest = null;
            } else if ((ActualStatus == Status.AwaitingConfirmation) && (_targetStatus == Status.Ready)) {
                _log.Info($"Confirmation period expired, going to state {nameof(Status.Ready)} without actually applying new configuration.");
                ActualStatus = Status.Ready;
                responseMessage = BuildConfReqResponseString("Error-Host did not confirm request in time");
                _udpSendQueue.Enqueue(new ClientRawMessage { Payload = responseMessage, Endpoint = _unansweredConfRequest.Endpoint });
                _unansweredConfRequest = null;
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Disabled)) {
                IsReconfigurationEnabled = false;
                ActualStatus = Status.Disabled;
                _log.Info($"Going to state {nameof(Status.Disabled)} upon host's request. LC-FIND is now disabled.");
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Cooldown)) {
                if (DateTime.Now >= _cooldownEnd) {
                    _targetStatus = Status.Ready;
                }
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Ready)) {
                _log.Info($"Cooldown period expired, going to state {nameof(Status.Ready)}.");
                ActualStatus = Status.Ready;
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Disabled)) {
                IsReconfigurationEnabled = false;
                ActualStatus = Status.Disabled;
                _log.Info($"Going to state {nameof(Status.Disabled)} upon host's request. LC-FIND is now disabled.");
            } else if ((ActualStatus == Status.Disabled) && (_targetStatus == Status.Ready)) {
                ActualStatus = Status.Ready;
                IsReconfigurationEnabled = true;
                _log.Info($"Going to state {nameof(Status.Ready)} upon host's request. LC-FIND is now enabled.");
            }
        }
    }
}
