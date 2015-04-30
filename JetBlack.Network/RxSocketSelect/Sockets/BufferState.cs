namespace JetBlack.Network.RxSocketSelect.Sockets
{
    public class BufferState
    {
        public BufferState(byte[] bytes, int offset, int length)
        {
            Bytes = bytes;
            Offset = offset;
            Length = length;
        }

        public byte[] Bytes { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
    }

    public static class BufferStateExtensions
    {
        public static BufferState Advance(this BufferState bufferState, int count)
        {
            bufferState.Offset += count;
            bufferState.Length -= count;
            return bufferState;
        }
    }
}
