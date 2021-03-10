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
- Additional data fields in FIND response: ```Status```, ```Result```, ```NetworkMode```, ```Mask```, ```Gateway```.
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
