using System;
using Google.Protobuf;

namespace Fixer.Network
{
    public static class ProtobufPacket
    {
        public static byte[] Build(ushort pktId, IMessage msg)
        {
            byte[] body = msg != null ? msg.ToByteArray() : Array.Empty<byte>();
            int size = NetCommon.HeaderSize + body.Length;

            if (size > NetCommon.MaxReceiveBufferLen)
                throw new Exception($"Packet too large: {size}");

            byte[] buffer = new byte[size];

            BitConverter.GetBytes(pktId).CopyTo(buffer, 0);
            BitConverter.GetBytes((ushort)size).CopyTo(buffer, 2);

            if (body.Length > 0)
                Buffer.BlockCopy(body, 0, buffer, 4, body.Length);

            return buffer;
        }
    }
}
