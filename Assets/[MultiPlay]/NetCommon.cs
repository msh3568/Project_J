namespace Fixer.Network
{
    public static class NetCommon
    {
        public const int HeaderSize = 4;                  // uint16 pkt_id + uint16 pkt_size
        public const int MaxReceiveBufferLen = 2048;      // MAX_RECEIVE_BUFFER_LEN
    }
}
