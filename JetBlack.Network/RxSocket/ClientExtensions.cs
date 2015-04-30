using System;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using JetBlack.Network.Common;

namespace JetBlack.Network.RxSocket
{
    public static class ClientExtensions
    {
        public static ISubject<ByteBuffer, ByteBuffer> ToClientSubject(this Socket socket, int size, SocketFlags socketFlags)
        {
            return Subject.Create(socket.ToClientObserver(size, socketFlags), socket.ToClientObservable(size, socketFlags));
        }

        public static IObservable<ByteBuffer> ToClientObservable(this Socket socket, int size, SocketFlags socketFlags)
        {
            return Observable.Create<ByteBuffer>(async (observer, token) =>
            {
                var buffer = new byte[size];

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var received = await socket.ReceiveAsync(buffer, 0, size, socketFlags);
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

        public static IObserver<ByteBuffer> ToClientObserver(this Socket socket, int size, SocketFlags socketFlags)
        {
            return Observer.Create<ByteBuffer>(async buffer =>
            {
                var sent = 0;
                while (sent < buffer.Length)
                    sent += await socket.SendAsync(buffer.Bytes, sent, buffer.Length - sent, socketFlags);
            });
        }
    }
}
