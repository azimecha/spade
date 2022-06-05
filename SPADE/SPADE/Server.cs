using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace SPADE {
    public class Server : IDisposable {
        private Thread _thdServer;
        private UdpClient _sock;
        private IPEndPoint _ipep;
        private ManualResetEvent _evtStop;
        private AutoResetEvent _evtPacketRecvd;
        private Dictionary<IPAddress, UnconfirmedResponseInfo> _dicUnconfirmed;
        private RandomNumberGenerator _rng;

        public Server(IPEndPoint ipep) {
            _ipep = ipep;
            _sock = new UdpClient(ipep);
            _evtStop = new ManualResetEvent(false);
            _evtPacketRecvd = new AutoResetEvent(false);
            _dicUnconfirmed = new Dictionary<IPAddress, UnconfirmedResponseInfo>(new AddressEqualityComparer());
            _rng = RandomNumberGenerator.Create();

            _thdServer = new Thread(ServerThreadProc);
            _thdServer.Name = "SPADE Server";
            _thdServer.Start();
        }

        public delegate void ErrorDelegate(Server srv, Exception ex);
        public event ErrorDelegate FatalError;
        public event ErrorDelegate NonfatalError;

        public delegate void InfoDelegate(Server srv, IPEndPoint ipepRemote);
        public event InfoDelegate ReceivedRequest;
        public event InfoDelegate SentResponse;
        public event InfoDelegate ReceivedConfirmation;
        public event InfoDelegate ResponseConfirmed;

        public IPEndPoint Endpoint => _ipep;

        private void ServerThreadProc() {
            try {
                while (true) {
                    _sock.BeginReceive(PacketReceivedCallback, null);

                    switch (WaitHandle.WaitAny(new WaitHandle[] { _evtPacketRecvd, _evtStop })) {
                        case 0:
                            continue;

                        case 1:
                            break;
                    }
                }
            } catch (Exception ex) {
                FatalError?.Invoke(this, ex);
            }
        }

        private void PacketReceivedCallback(IAsyncResult result) {
            IPEndPoint ipepRemote = _ipep;
            byte[] arrPacket = _sock.EndReceive(result, ref ipepRemote);

            try {
                ProcessPacket(arrPacket, ipepRemote);
            } catch (Exception ex) {
                NonfatalError?.Invoke(this, ex);
            }

            _evtPacketRecvd.Set();
        }

        private static readonly TimeSpan DECLINE_TIMER = new TimeSpan(0, 0, 30);

        private unsafe void ProcessPacket(byte[] arrPacket, IPEndPoint ipepRemote) {
            if (arrPacket.Length < sizeof(Protocol.Header))
                throw new FormatException("Packet too short");

            fixed (byte* pPacket = arrPacket) {
                Protocol.Header* pHeader = (Protocol.Header*)pPacket;
                if (pHeader->MagicValue != Protocol.Constants.MAGIC_VALUE)
                    throw new FormatException("Magic value does not match");
                if (pHeader->ProtocolVersion != 1)
                    return;

                switch (pHeader->TypeID) {
                    case Protocol.PacketType.RequestPacket:
                        ProcessRequestPacket(*pHeader, ipepRemote);
                        break;

                    case Protocol.PacketType.ResponsePacket:
                        throw new FormatException("Client sent response packet");

                    case Protocol.PacketType.ConfirmationPacket:
                        if (arrPacket.Length < sizeof(Protocol.Confirmation))
                            throw new FormatException("Confirmation packet too short");
                        ProcessConfirmationPacket(*(Protocol.Confirmation*)pPacket, ipepRemote);
                        break;

                    default:
                        throw new FormatException($"Invalid packet type {pHeader->TypeID}");
                }
            }
        }

        private unsafe void ProcessRequestPacket(Protocol.Header packet, IPEndPoint ipepRemote) {
            ReceivedRequest?.Invoke(this, ipepRemote);

            if (_dicUnconfirmed.TryGetValue(ipepRemote.Address, out UnconfirmedResponseInfo infUnconf)) {
                if ((DateTime.Now - infUnconf.RespondedAt) < DECLINE_TIMER)
                    throw new InvalidOperationException($"{ipepRemote.Address} requested again without confirmation (last request was at {infUnconf.RespondedAt})");
                else
                    _dicUnconfirmed.Remove(ipepRemote.Address); // timeout expired
            }

            byte[] arrRequestToken = new byte[Protocol.Constants.TOKEN_SIZE];
            Marshal.Copy((IntPtr)packet.RequestToken, arrRequestToken, 0, Protocol.Constants.TOKEN_SIZE);

            byte[] arrRespToken = new byte[Protocol.Constants.TOKEN_SIZE];
            _rng.GetBytes(arrRespToken);

            byte[] arrRemoteAddr = ipepRemote.Address.GetAddressBytes();
            Utils.Invert(arrRemoteAddr);

            byte[] arrResponse = new byte[sizeof(Protocol.Response) + arrRemoteAddr.Length];

            fixed (byte* pRespData = arrResponse) {
                Protocol.Response* pResponse = (Protocol.Response*)pRespData;
                pResponse->Header.MagicValue = Protocol.Constants.MAGIC_VALUE;
                pResponse->Header.ProtocolVersion = 1;
                pResponse->Header.TypeID = Protocol.PacketType.ResponsePacket;

                Marshal.Copy(arrRequestToken, 0, (IntPtr)pResponse->Header.RequestToken, Protocol.Constants.TOKEN_SIZE);
                Marshal.Copy(arrRespToken, 0, (IntPtr)pResponse->ResponseToken, Protocol.Constants.TOKEN_SIZE);

                pResponse->PublicPort = unchecked((ushort)~(uint)ipepRemote.Port);
                Marshal.Copy(arrRemoteAddr, 0, (IntPtr)(pRespData + sizeof(Protocol.Response)), arrRemoteAddr.Length);
            }

            _sock.Send(arrResponse, arrResponse.Length, ipepRemote);
            _dicUnconfirmed.Add(ipepRemote.Address, new UnconfirmedResponseInfo(arrRequestToken, arrRespToken));

            SentResponse?.Invoke(this, ipepRemote);
        }

        private unsafe void ProcessConfirmationPacket(Protocol.Confirmation packet, IPEndPoint ipepRemote) {
            ReceivedConfirmation?.Invoke(this, ipepRemote);

            UnconfirmedResponseInfo infUnconf;
            if (!_dicUnconfirmed.TryGetValue(ipepRemote.Address, out infUnconf))
                throw new InvalidOperationException($"Received confirmation from {ipepRemote.Address} without outstanding request");

            if (!Utils.IsDataEqual(infUnconf.RequestToken, packet.Header.RequestToken))
                throw new InvalidOperationException($"Confirmation from {ipepRemote.Address} contained incorrect request token");

            if (!Utils.IsDataEqual(infUnconf.ResponseToken, packet.ResponseToken))
                throw new InvalidOperationException($"Confirmation from {ipepRemote.Address} contained incorrect response token");

            _dicUnconfirmed.Remove(ipepRemote.Address);

            ResponseConfirmed?.Invoke(this, ipepRemote);
        }

        private struct UnconfirmedResponseInfo {
            public DateTime RespondedAt;
            public byte[] RequestToken;
            public byte[] ResponseToken;

            public UnconfirmedResponseInfo(byte[] arrReqToken, byte[] arrRespToken) {
                RespondedAt = DateTime.Now;
                RequestToken = arrReqToken;
                ResponseToken = arrRespToken;
            }
        }
 
        public void Dispose() {
            _evtStop.Set();
            _thdServer.Join();
            Interlocked.Exchange(ref _sock, null)?.Close();
        }
    }
}
