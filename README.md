# Introduction

LC-FIND protocol is designed to help remotely detect and change network configuration of devices that are connected to local area network but don't have displays and/or buttons to change or view such configuration.

Typically, network devices operate using statically or dynamically assigned IP address. In order for applications to connect to such device, IP address has to be known. Problem is that in both modes (static or dynamic) there is no user-friendly way of knowing such address. In case static address is used, it must be hard-coded in some place like user manual where user could look. However, user may be unhappy with such address for number of reasons and change it. What if he forgets it or accidentally changes it to unreachable address? In case dynamic address is used, user must check administration panel of the router an search the list of distributed IP address. This is doable, but also not user friendly. Additionally, in organizations, most users don't have access to their router configuration.

How easy could it be, if every device would simply send a response, if the user asks for the name and the IP address of each device in his network? LC-FIND protocol does exactly that.

> This is extended but compatible version of [SEGGER's FIND protocol](https://www.segger.com/products/connectivity/emnet/technology/find-protocol/). Meaning that software tools provided by SEGGER or 3rd party tools written for SEGGER's FIND protocol is still able to communicate with devices implementing LC-FIND protocol. 

# SEGGER's FIND specification

The protocol is simple and efficient. A host sends a query via UDP broadcast to port 50022 and all clients which are listening on that port send a UDP unicast response with the used address configuration to port 50022.

- The maximum size of the payload of packets (query/response) is 512 bytes.
- The payload of requests and responses is a zero-terminated UTF-8 string.
- Payload consist of key-value pairs ```{key}={value};{key}={value};```. Here "=" and ";" are used as delimiters, so those symbols cannot be used in the actual payload.

Minimal mandatory request is:

```
FINDReq=1;
```

And minimal mandatory response:

```
FIND=1;SN={serialNumber};HWADDR={MAC};DeviceName={DeviceName};
```

Where mandatory structure of "FINDreq" is:

Key|Valid values|Description
---|------------|-----------
FINDReq|1|Find request version 1.

And mandatory structure of "FIND" is:

Key|Valid values|Description
---|------------|-----------
FIND|1|Indicates that this is a response to FINDReq=1; message.
SN|Any string without characters "=" or ";"|Serial number of the device. Must be unique.
HWADDR|MAC address string in format AA:BB:CC:DD:EE:FF|MAC address of the device.
DeviceName|Any string without characters "=" or ";"|Name of the device which should help user to know what kind of device this is. There can be multiple devices with the same name.

# LC-FIND extension

SEGGER's FIND protocol has an issue that it doesn't work when client and device are in different subnets. This is because SEGGER specifies that responses from the device must come as a UDP unicast. Also, there is no way of changing device configuration. So, LC-FIND adds theses changes to original protocol:

- Instead of unicast responses, LC-FIND uses broadcast responses.
- Additional data fields in FIND response: ```NetworkMode```, ```Mask```, ```Gateway```.
- New ```CONFReq``` request and ```CONF``` response messages. 

## Using broadcast responses instead of unicast ones

All messages, including responses, now use UDP broadcasts. Everything else stays the same. For example, client sends this UDP broadcast to IP 255.255.255.255, port 50022:

```
FINDReq=1;
```
And the host replies to IP 255.255.255.255, port 50022:

```
FIND=1;SN={serialNumber};HWADDR={MAC};DeviceName={DeviceName};
```

## Adding additional data in responses

LC-FIND can be used to change configuration of the device, so additional data fields had to be added to FIND=1 response so that user could know full information about current device configuration:

Key|Valid values|Description
---|------------|-----------
NetworkMode|"DHCP" or "Static"|DHCP means that device is a DHCP client and receives its IP configuration dynamically from the router. Static means that device assigns itself a custom and configurable IP address, mask and gateway. 
Mask|IP address string in format "x.x.x.x"|Currently used IPv4 subnet mask.
Gateway|IP address string in format "x.x.x.x"|Currently used IPv4 subnet mask.

So a typical response now looks like this:

```
FIND=1;SN=123456;HWADDR=AA:BB:CC:DD:EE:FF;DeviceName=Laser;NetworkMode=Static;Mask=255.255.255.0;Gateway=0.0.0.0;
```

# New CONFReq and CONF messages

SEGGER FIND is only about detecting devices. LC-FIND add a CONFReq request and CONF response messages, which are used to change device's network configuration.

A typical successful command-response pair would like this:

    CONFReq=1;HWADDR=AA:BB:CC:DD:EE:FF;NetworkMode=DHCP;
    CONF=1;HWADDR=AA:BB:CC:DD:EE:FF;Status=AwaitingConfirmation;Result=Ok;

If there was a problem of some sort, message exchange will look something like:

    CONFReq=1;HWADDR=AA:BB:CC:DD:EE:FF;NetworkMode=Static;IP=192.168.11.251;Mask=255.255.255.0;Gateway=0.0.0.0;
    CONF=1;HWADDR=AA:BB:CC:DD:EE:FF;Status=Ready;Result=Error-This IP address is reserved and cannot be used;

Mandatory structure of "CONFReq" is:

Key|Valid values|Description
---|------------|-----------
CONFReq|1|Request to change device configuration. Version 1.
HWADDR|MAC address string in format AA:BB:CC:DD:EE:FF|MAC address of the device.
NetworkMode|"DHCP" or "Static"|DHCP means that device is a DHCP client and receives its IP configuration dynamically from the router. Static means that device assigns itself a custom and configurable IP address, mask and gateway. 
IP|IP address string in format "x.x.x.x"|New static IP address. Only relevant if NetworkMode=Static.
Mask|IP address string in format "x.x.x.x"|New subnet mask. Only relevant if NetworkMode=Static.
Gateway|IP address string in format "x.x.x.x"|New gateway address. Only relevant if NetworkMode=Static.

And then response fields are:

Key|Valid values|Description
---|------------|-----------
CONF|1|Indicates that this is a response to "CONFReq=1;" message.
HWADDR|MAC address string in format AA:BB:CC:DD:EE:FF|MAC address of the device that processed CONFReq message.
Status|"Ready", "AwaitingConfirmation", "Cooldown", "Disabled"|Ok - Reconfiguration succeeded. AwaitingConfirmation - Device waits for a physical confirmation to allow reconfiguration (e.g. a button press) (can be omitted if no means of physically confirming reconfiguration (e.g. a button) are present). Cooldown - Device is in a cooldown state and will not accept configuration requests for a while. Disabled - LC-FIND is disabled on host and it will not accept configuration request until enabled.
Result|Any text string|Gives more information about errors. Actual content is not part of this specification.