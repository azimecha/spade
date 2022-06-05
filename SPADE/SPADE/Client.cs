using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace SPADE {
    public class Client : IDisposable {
        private IPEndPoint _ipep;
        private Mutex _mtxClientUse;
        private RandomNumberGenerator _rng;
        private AutoResetEvent _evtResponseRecvd;
        private ManualResetEvent _evtNeverSignaled;

        public Client(int nPort) : this(new IPEndPoint(IPAddress.Any, nPort)) { }

        public Client(IPEndPoint ipep) {
            _ipep = ipep;
            _mtxClientUse = new Mutex();
            _rng = RandomNumberGenerator.Create();
            _evtResponseRecvd = new AutoResetEvent(false);
            _evtNeverSignaled = new ManualResetEvent(false);

            Timeout = new TimeSpan(0, 0, 10);
        }

        public TimeSpan Timeout { get; set; }

        private const int WSAHOST_NOT_FOUND = 11001; // "Host not found. No such host is known." - MSDN
        private const int WSANO_DATA = 11004; // "Valid name, no data record of requested type." - MSDN

        public IPEndPoint PerformTransaction(string strServerHostname, int nPort, WaitHandle whCancel = null) {
            if (IPAddress.TryParse(strServerHostname, out IPAddress addrFromString))
                return PerformTransaction(new IPEndPoint(addrFromString, nPort), whCancel);

            IPAddress[] arrAddrs = Dns.GetHostAddresses(strServerHostname);

            foreach (IPAddress addr in arrAddrs) {
                if (addr.AddressFamily == _ipep.AddressFamily)
                    return PerformTransaction(new IPEndPoint(addr, nPort), whCancel);
            }

            throw new SocketException(WSANO_DATA);
        }

        public unsafe IPEndPoint PerformTransaction(IPEndPoint ipepServer, WaitHandle whCancel = null) {
            if (whCancel is null)
                whCancel = _evtNeverSignaled;

            // acquire mutex
            switch (WaitHandle.WaitAny(new WaitHandle[] { _mtxClientUse, whCancel })) {
                case 0:
                    break;

                case 1:
                    throw new OperationCanceledException();
            }

            try {
                // open socket
                UdpClient sock = new UdpClient(_ipep);
                try {
                    sock.Connect(ipepServer);

                    // send request
                    byte[] arrReqToken = new byte[Protocol.Constants.TOKEN_SIZE];
                    _rng.GetBytes(arrReqToken);

                    Protocol.Header packetRequest = CreatePacket<Protocol.Header>(Protocol.PacketType.RequestPacket, arrReqToken);
                    byte[] arrReqData = GetBytes(packetRequest);
                    sock.Send(arrReqData, arrReqData.Length);

                    // get response
                    byte[] arrResponse = null;

                    while (arrResponse is null) {
                        sock.BeginReceive(result => {
                            IPEndPoint ipepRemote = _ipep;
                            arrResponse = sock.EndReceive(result, ref ipepRemote);

                            if (ipepRemote.ToString() != ipepServer.ToString())
                                arrResponse = null;

                            _evtResponseRecvd.Set();
                        }, null);

                        switch (WaitHandle.WaitAny(new WaitHandle[] { _evtResponseRecvd, whCancel }, Timeout)) {
                            case 0:
                                break;

                            case 1:
                                throw new OperationCanceledException();

                            case WaitHandle.WaitTimeout:
                                throw new TimeoutException();
                        }
                    }

                    // check response
                    byte[] arrIPAddress;
                    Protocol.Response packetResponse = ParseResponse(arrResponse, out arrIPAddress);
                    if (!Utils.IsDataEqual(arrReqToken, packetResponse.Header.RequestToken))
                        throw new FormatException("Server returned incorrect request token");

                    IPEndPoint ipepPublic = new IPEndPoint(new IPAddress(arrIPAddress), packetResponse.PublicPort);

                    // send confirmation
                    Protocol.Confirmation packetConfirm = CreatePacket<Protocol.Confirmation>(Protocol.PacketType.ConfirmationPacket, arrReqToken);
                    Utils.Copy(packetResponse.ResponseToken, packetConfirm.ResponseToken, Protocol.Constants.TOKEN_SIZE);
                    byte[] arrConfData = GetBytes(packetConfirm);
                    sock.Send(arrConfData, arrConfData.Length);

                    return ipepPublic;

                } finally {
                    sock.Close();
                }
            } finally {
                _mtxClientUse.ReleaseMutex();
            }
        }

        public static unsafe T CreatePacket<T>(Protocol.PacketType ptype, byte[] arrReqToken) where T : unmanaged {
            if (sizeof(T) < sizeof(Protocol.Header))
                throw new FormatException($"Cannot create packet of type {typeof(T).FullName}: size {sizeof(T)} less than header size {sizeof(Protocol.Header)}");

            T packet = new T();

            Protocol.Header* pHeader = (Protocol.Header*)&packet;
            pHeader->MagicValue = Protocol.Constants.MAGIC_VALUE;
            pHeader->ProtocolVersion = Protocol.Constants.PROTOCOL_VERSION;
            pHeader->TypeID = ptype;
            Marshal.Copy(arrReqToken, 0, (IntPtr)pHeader->RequestToken, Protocol.Constants.TOKEN_SIZE);

            return packet;
        }

        public static unsafe byte[] GetBytes<T>(T obj) where T : unmanaged {
            byte[] arrData = new byte[sizeof(T)];
            Marshal.Copy((IntPtr)(&obj), arrData, 0, sizeof(T));
            return arrData;
        }

        public static unsafe Protocol.Response ParseResponse(byte[] arrData, out byte[] arrIPAddress) {
            if (arrData.Length < sizeof(Protocol.Response))
                throw new FormatException($"{arrData.Length} byte packet is too small to be a response (must be {sizeof(Protocol.Response)} bytes)");

            arrIPAddress = new byte[arrData.Length - sizeof(Protocol.Response)];

            fixed (byte* pData = arrData) {
                Marshal.Copy((IntPtr)(pData + sizeof(Protocol.Response)), arrIPAddress, 0, arrIPAddress.Length);
                Utils.Invert(arrIPAddress);

                Protocol.Response resp = *(Protocol.Response*)pData;
                resp.PublicPort = unchecked((ushort)~(uint)resp.PublicPort);
                return resp;
            }
        }

        public void Dispose() {
        }
    }
}
