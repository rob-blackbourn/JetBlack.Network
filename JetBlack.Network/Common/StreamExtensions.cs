using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;

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
    }
}
