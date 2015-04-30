using System;

namespace JetBlack.Network.Common
{
    public class DisposableByteBuffer : ByteBuffer, IDisposable
    {
        private readonly IDisposable _disposable;

        public DisposableByteBuffer(byte[] bytes, int length, IDisposable disposable)
            : base(bytes, length)
        {
            if (disposable == null)
                throw new ArgumentNullException("disposable");
            _disposable = disposable;
        }

        public void Dispose()
        {
            _disposable.Dispose();
        }
    }
}
