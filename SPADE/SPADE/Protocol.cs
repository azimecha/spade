using System;
using System.Runtime.InteropServices;

namespace SPADE.Protocol {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct Header {
        public uint MagicValue;
        public byte ProtocolVersion;
        public PacketType TypeID;
        public fixed byte RequestToken[16];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct Response {
        public Header Header;
        public fixed byte ResponseToken[16];
        public ushort PublicPort;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct Confirmation {
        public Header Header;
        public fixed byte ResponseToken[16];
    }

    public enum PacketType : byte {
        RequestPacket, ResponsePacket, ConfirmationPacket
    }

    public static class Constants {
        public const uint MAGIC_VALUE = 0x44415053; // SPAD
        public const byte PROTOCOL_VERSION = 1;
        public const int TOKEN_SIZE = 16;
    }
}
