using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;

namespace JetBlack.Network.Common
{
    public static class StreamExtensions
    {
        public static IObservable<ByteBuffer> ToStreamObservable(this Stream stream, int size)
        {
            return Observable.Create<ByteBuffer>(async (observer, token) =>
            {
                var buffer = new byte[size];

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var received = await stream.ReadAsync(buffer, 0, size, token);
                        if (received == 0)
                            break;

                        observer.OnNext(new ByteBuffer(buffer, received));
                    }

                    observer.OnCompleted();
                }
                catch (Exception error)
                {
                    observer.OnError(error);
                }
            });
        }

        public static IObserver<ByteBuffer> ToStreamObserver(this Stream stream, CancellationToken token)
        {
            return Observer.Create<ByteBuffer>(async buffer =>
            {
                await stream.WriteAsync(buffer.Bytes, 0, buffer.Length, token);
            });
        }

        public static IObservable<DisposableByteBuffer> ToFrameStreamObservable(this Stream stream, BufferManager bufferManager)
        {
            return Observable.Create<DisposableByteBuffer>(async (observer, token) =>
            {
                var headerBuffer = new byte[sizeof(int)];

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (await stream.ReadBytesCompletelyAsync(headerBuffer, headerBuffer.Length, token) != headerBuffer.Length)
                            break;
                        var length = BitConverter.ToInt32(headerBuffer, 0);

                        var buffer = bufferManager.TakeBuffer(length);
                        if (await stream.ReadBytesCompletelyAsync(buffer, length, token) != length)
                            break;

                        observer.OnNext(new DisposableByteBuffer(buffer, length, Disposable.Create(() => bufferManager.ReturnBuffer(buffer))));
                    }

                    observer.OnCompleted();
                }
                catch (Exception error)
                {
                    observer.OnError(error);
                }
            });
        }

        public static IObserver<DisposableByteBuffer> ToFrameStreamObserver(this Stream stream, CancellationToken token)
        {
            return Observer.Create<DisposableByteBuffer>(async managedBuffer =>
            {
                var headerBuffer = BitConverter.GetBytes(managedBuffer.Length);
                await stream.WriteAsync(headerBuffer, 0, headerBuffer.Length, token);
                await stream.WriteAsync(managedBuffer.Bytes, 0, managedBuffer.Length, token);
            });
        }

        public static async Task<int> ReadBytesCompletelyAsync(this Stream stream, byte[] buf, int length, CancellationToken token)
        {
            var read = 0;
            while (read < length)
            {
                var remaining = length - read;
                var bytes = await stream.ReadAsync(buf, read, remaining, token);
                if (bytes == 0)
                    return read;

                read += bytes;
            }
            return read;
        }
    }

}
