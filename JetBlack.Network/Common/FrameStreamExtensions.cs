using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;

namespace JetBlack.Network.Common
{
    public static class FrameStreamExtensions
    {
        public static ISubject<DisposableValue<ArraySegment<byte>>, DisposableValue<ArraySegment<byte>>> ToFrameStreamSubject(this Stream stream, BufferManager bufferManager, CancellationToken token)
        {
            return Subject.Create(stream.ToFrameStreamObserver(token), stream.ToFrameStreamObservable(bufferManager));
        }

        public static IObservable<DisposableValue<ArraySegment<byte>>> ToFrameStreamObservable(this Stream stream, BufferManager bufferManager)
        {
            return Observable.Create<DisposableValue<ArraySegment<byte>>>(async (observer, token) =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var buffer = await stream.ReadHeader(token)
                            .ContinueWith(task => stream.ReadBody(task.Result, bufferManager, token), token);

                        if (buffer.Result == DisposableValue<ArraySegment<byte>>.Empty)
                            break;

                        observer.OnNext(buffer.Result);
                    }

                    observer.OnCompleted();
                }
                catch (Exception error)
                {
                    observer.OnError(error);
                }
            });
        }

        private static async Task<int> ReadHeader(this Stream stream, CancellationToken token)
        {
            var headerBuffer = new byte[sizeof(int)];

            if (await stream.ReadBytesCompletelyAsync(headerBuffer, headerBuffer.Length, token) != headerBuffer.Length)
                return -1;

            return BitConverter.ToInt32(headerBuffer, 0);
        }

        private static async Task<DisposableValue<ArraySegment<byte>>> ReadBody(this Stream stream, int length, BufferManager bufferManager, CancellationToken token)
        {
            if (length <= 0)
                return DisposableValue<ArraySegment<byte>>.Empty;

            var buffer = bufferManager.TakeBuffer(length);
            if (await stream.ReadBytesCompletelyAsync(buffer, length, token) != length)
                return DisposableValue<ArraySegment<byte>>.Empty;

            return DisposableValue.Create(new ArraySegment<byte>(buffer, 0, length), Disposable.Create(() => bufferManager.ReturnBuffer(buffer)));
        }

        public static IObserver<DisposableValue<ArraySegment<byte>>> ToFrameStreamObserver(this Stream stream, CancellationToken token)
        {
            return Observer.Create<DisposableValue<ArraySegment<byte>>>(async disposableBuffer =>
            {
                var headerBuffer = BitConverter.GetBytes(disposableBuffer.Value.Count);

                await Task.WhenAll(new Task[]
                    {
                        stream.WriteAsync(headerBuffer, 0, headerBuffer.Length, token),
                        stream.WriteAsync(disposableBuffer.Value.Array, 0, disposableBuffer.Value.Count, token),
                        stream.FlushAsync(token)
                    });
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
