namespace JetBlack.Network.Common
{
    public class ByteBuffer
    {
        public ByteBuffer(byte[] bytes, int length)
        {
            Bytes = bytes;
            Length = length;
        }

        public byte[] Bytes { get; private set; }
        public int Length { get; private set; }
    }
}
