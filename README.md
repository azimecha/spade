This repository contains implementations of the server and client parts of a
protocol designed to be used for determining a device's public IP address.
The library is compatible with .NET 5+, .NET Core 2.0+, and .NET Framework
2.0+.

# Simple Public Address Detection (SPADE) Protocol

## Introduction

With IPv4 address exhaustion having occurred several years ago, almost all
Internet devices are now behind some type of network address translation,
possibly multiple layers of it.  In order for two devices to exchange packets,
it is necessary to 'punch' through any NAT layers using methods such as UDP
holepunching.  The first step in performing such a holepunch is to determine
the public address and port that a private address and local port will
be mapped to by the NAT device.

Existing systems of determining the device's external address and port, 
such as STUN, TURN, and ICE, are complex, requiring multiple messages to be
exchanged over TCP or even TLS connections.  The complexity of these protocols
results in a high cost to the server operator, so those systems generally
require authentication, with each server being operated by a software provider
for that provider's applications only.  This is undesirable for open source
projects, as there may be many forks of the project maintained by different
parties.

An alternative model can be observed in how BitTorrent trackers operate.
These servers include no authentication and can be used by anyone.  This
removes the necessity for the creators of torrent clients to operate 
centralized servers, and has allowed an ecosystem of open source clients
to flourish.  Such an open system is made possible by the simplicity 
of the torrent tracker protocol.  The low number of messages which must be
exchanged between the torrent client and tracker, and the infrequency of the
communication, mean that public trackers can be operated at a reasonable cost.

Although it may be possible for a device to discover its external IP address
using various public services, there is currently no way to do the same for
the device's public UDP port, which is vital for holepunching.  The SPADE
protocol is intended to provide this service using the same simple, public,
no-authentication model that torrent trackers use.

## Protocol overview

The protocol consists of packets sent between a client and server using UDP.
The client may be behind one or more layers of NAT.  The server is connected
directly to the Internet.

A SPADE transaction consists of the following steps:
1. The client sends a Request packet to the server.
2. The server sends a Response packet to the client.
3. The client sends a Confirmation packet to the server.

The Response packet contains the client's public IP address and port. 

The Request packet has this structure, which is also a common header for
all the different packets:

| Offset | Size | Type           | Description                  |
|--------|------|----------------|------------------------------|
|      0 |    4 | ASCII text     | The four characters 'SPAD'   |
|      4 |    1 | Unsigned int   | Protocol version: 1          |
|      5 |    1 | Unsigned int   | Packet type                  |
|      6 |   16 | Binary data    | Request token (random)       |

The Response packet adds a response token and the address information:

| Offset | Size | Type           | Description                   |
|--------|------|----------------|-------------------------------|
|      0 |    4 | ASCII text     | The four characters 'SPAD'    |
|      4 |    1 | Unsigned int   | Protocol version: 1           |
|      5 |    1 | Unsigned int   | Packet type                   |
|      6 |   16 | Binary data    | Request token                 |
|     22 |   16 | Binary data    | Response token (random)       |
|     38 |    2 | Unsigned int   | Client's public port inverted |
|     40 |    ? | Binary data    | Client's IP address inverted  |

The Confirmation packet is just the common header and response token:

| Offset | Size | Type           | Description                  |
|--------|------|----------------|------------------------------|
|      0 |    4 | ASCII text     | The four characters 'SPAD'   |
|      4 |    1 | Unsigned int   | Protocol version: 1          |
|      5 |    1 | Unsigned int   | Packet type                  |
|      6 |   16 | Binary data    | Request token                |
|     22 |   16 | Binary data    | Response token               |

The Confirmation packet and request/response tokens are used to prevent
redirected denial-of-service attacks.  The token values should be true 
random numbers.

The IP address field in the Response packet will vary in size depending
on whether IPv4 or IPv6 is being used.  The port and address are inverted
using bitwise NOT to prevent rare cases of routers manipulating values
inside the packet.

## Exact specification

The SPADE protocol SHALL consist of a Transaction between a Client and a
Server.  This Transaction SHALL consist of packets sent using the User
Datagram Protocol (UDP).

All packets begin with the four US-ASCII characters 'SPAD' as a magic value
and a byte with the decimal value 1 (one) to indicate the protocol version.
Both the Client and Server SHALL ignore any packets which do not begin with 
the aforementioned magic value.  All protocol versions other than 1 (one)
are currently reserved; the Client and Server SHALL ignore any packets
containing a protocol version with which they are not compatible.

The Client SHALL initiate the Transaction by transmitting a Request packet
to the Server.

The Request packet SHALL consist of the following:
1. The four US-ASCII characters 'SPAD'.
2. A single byte with the decimal value 1 (one) to indicate the protocol
   version.
3. A single byte with the decimal value 1 (one) to indicate the type of
   packet.
4. A Request Token consisting of sixteen bytes of data.  This data SHOULD
   be a cryptographically secure random value.

Upon receiving a Request packet, the Server MAY respond with a Response
packet.  If a Client submits an excessive number of Request packets, the
Server SHOULD ignore further Request packets from that Client for a
period of time.

The Response packet SHALL consist of the following:
1. The four US-ASCII characters 'SPAD'.
2. A single byte with the decimal value 1 (one) to indicate the protocol
   version.
3. A single byte with the decimal value 2 (two) to indicate the type of
   packet.
4. The Request Token from the Request packet.
5. A Response Token consisting of sixteen bytes of data.  This data SHOULD
   be a cryptographically secure random value.
6. An unsigned 16-bit little endian integer containing the bitwise inverse
   of the port from which the Client sent the Request packet.
7. The bitwise inverse of the Internet address from which the Client
   sent the Request packet.

Upon receiving a Response packet, the Client SHALL compare the Token from
that packet to the Token it sent in the Request packet.  If any difference
exists, the Client SHALL ignore the Response packet.  Otherwise, the Client
SHALL transmit a Confirmation packet.

If the Client receives a Response packet from a server to which it has
not transmitted a Request packet, it SHALL ignore and discard the packet.

The Confirmation packet SHALL consist of the following:
1. The four US-ASCII characters 'SPAD'.
2. A single byte with the decimal value 1 (one) to indicate the protocol
   version.
3. A single byte with the decimal value 3 (three) to indicate the type of
   packet.
4. The Request Token from the Request packet.
5. The Response Token from the Response packet.

Upon receiving a Confirmation packet, the Server SHALL compare the provided
Request Token and Response Token to the values it sent in the Response packet.
If any difference exists, the Server SHALL consider the Confirmation packet
invalid and ignore it.  Otherwise, it SHALL consider the Confirmation packet
to be valid.

If the Server receives a valid Confirmation packet within a reasonable amount
of time, it SHOULD consider the Transaction to be confirmed.  Otherwise,
it SHOULD consider the Transaction to be declined.  If a Server receives a
large number of requests from a certain Client which are then declined, the
Server SHOULD ignore further requests from the Client.









