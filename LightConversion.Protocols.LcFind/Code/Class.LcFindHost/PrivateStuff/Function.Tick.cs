// Copyright 2021 Light Conversion, UAB
// Licensed under the Apache 2.0, see LICENSE.md for more details.

using System;

namespace LightConversion.Protocols.LcFind {
    public partial class LcFindHost {
        private void Tick() {
            if ((ActualStatus == Status.Ready) && (_targetStatus == Status.Ready)) {
                // bybis.
            } else if ((ActualStatus == Status.Ready) && (_targetStatus == Status.Disabled)) {
                ActualStatus = Status.Disabled;
            } else if ((ActualStatus == Status.Ready) && (_targetStatus == Status.Cooldown)) {
                var requestResult = "";

                if (_trySetNetworkConfigurationDelegate(_configurationToSet)) {
                    _cooldownEnd = DateTime.Now.AddSeconds(CooldownTimeout);
                    requestResult = "Ok";
                    ActualStatus = Status.Cooldown;
                } else {
                    requestResult = "Unable to set requested configuration";
                    ActualStatus = Status.Ready;
                }

                var response = BuildConfReqResponseString(requestResult);
                SendResponse(response, _remoteEndpoint);
                ActualStatus = Status.Cooldown;
            } else if ((ActualStatus == Status.Ready) && (_targetStatus == Status.AwaitingConfirmation)) {
                // todo.
            } else if ((ActualStatus == Status.Ready) && (_targetStatus == Status.Disabled)) {
                IsReconfigurationEnabled = false;
                ActualStatus = Status.Disabled;
            } else if ((ActualStatus == Status.AwaitingConfirmation) && (_targetStatus == Status.AwaitingConfirmation)) {
                // todo.
            } else if ((ActualStatus == Status.AwaitingConfirmation) && (_targetStatus == Status.Cooldown)) {
                // todo.
            } else if ((ActualStatus == Status.AwaitingConfirmation) && (_targetStatus == Status.Ready)) {
                // todo.
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Disabled)) {
                IsReconfigurationEnabled = false;
                ActualStatus = Status.Disabled;
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Cooldown)) {
                if (DateTime.Now >= _cooldownEnd) {
                    _targetStatus = Status.Ready;
                }
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Ready)) {
                ActualStatus = Status.Ready;
            } else if ((ActualStatus == Status.Cooldown) && (_targetStatus == Status.Disabled)) {
                IsReconfigurationEnabled = false;
                ActualStatus = Status.Disabled;
            } else if ((ActualStatus == Status.Disabled) && (_targetStatus == Status.Ready)) {
                ActualStatus = Status.Ready;
                IsReconfigurationEnabled = true;
            }
        }
    }
}