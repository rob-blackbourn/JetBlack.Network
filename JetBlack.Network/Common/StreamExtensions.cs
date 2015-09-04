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
        public static IObservable<ArraySegment<byte>> ToStreamObservable(this Stream stream, int size)
        {
            return Observable.Create<ArraySegment<byte>>(async (observer, token) =>
            {
                var buffer = new byte[size];

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var received = await stream.ReadAsync(buffer, 0, size, token);
                        if (received == 0)
                            break;

                        observer.OnNext(new ArraySegment<byte>(buffer, 0, received));
                    }

                    observer.OnCompleted();
                }
                catch (Exception error)
                {
                    observer.OnError(error);
                }
            });
        }

        public static IObserver<ArraySegment<byte>> ToStreamObserver(this Stream stream, CancellationToken token)
        {
            return Observer.Create<ArraySegment<byte>>(async buffer =>
            {
                await stream.WriteAsync(buffer.Array, buffer.Offset, buffer.Count, token);
            });
        }

        public static IObservable<DisposableValue<ArraySegment<byte>>> ToFrameStreamObservable(this Stream stream, BufferManager bufferManager)
        {
            return Observable.Create<DisposableValue<ArraySegment<byte>>>(async (observer, token) =>
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

                        observer.OnNext(DisposableValue.Create(new ArraySegment<byte>(buffer, 0, length), Disposable.Create(() => bufferManager.ReturnBuffer(buffer))));
                    }

                    observer.OnCompleted();
                }
                catch (Exception error)
                {
                    observer.OnError(error);
                }
            });
        }

        public static IObserver<DisposableValue<ArraySegment<byte>>> ToFrameStreamObserver(this Stream stream, CancellationToken token)
        {
            return Observer.Create<DisposableValue<ArraySegment<byte>>>(async disposableBuffer =>
            {
                var headerBuffer = BitConverter.GetBytes(disposableBuffer.Value.Count);
                await stream.WriteAsync(headerBuffer, 0, headerBuffer.Length, token);
                await stream.WriteAsync(disposableBuffer.Value.Array, 0, disposableBuffer.Value.Count, token);
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
