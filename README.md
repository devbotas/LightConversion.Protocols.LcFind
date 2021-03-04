# Introduction

LC-FIND protocol is designed to help remotely detect and change network configuration of devices that are connected to local area network but don't have displays and/or buttons to change or view such configuration.

Typically, network devices operate using statically or dynamically assigned IP address. In order for applications to connect to such device, IP address has to be known. Problem is that in both modes (static or dynamic) there is no user-friendly way of knowing such address. In case static address is used, it must be hard-coded in some place like user manual where user could look. However, user may be unhappy with such address for number of reasons and change it. What if he forgets it or accidentally changes it to unreachable address? In case dynamic address is used, user must check administration panel of the router an search the list of distributed IP address. This is doable, but also not user friendly. Additionally, in organizations, most users don't have access to their router configuration.

How easy could it be, if every device would simply send a response, if the user asks for the name and the IP address of each device in his network? LC-FIND protocol does exactly that.

This is extended but compatible version of SEGGER's FIND protocol. Meaning that software tools provided by SEGGER or 3rd party tools written for SEGGER's FIND protocol is still able to communicate with devices implementing LC-FIND protocol. 