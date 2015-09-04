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
        public static ISubject<ArraySegment<byte>, ArraySegment<byte>> ToClientSubject(this Socket socket, int size, SocketFlags socketFlags)
        {
            return Subject.Create(socket.ToClientObserver(size, socketFlags), socket.ToClientObservable(size, socketFlags));
        }

        public static IObservable<ArraySegment<byte>> ToClientObservable(this Socket socket, int size, SocketFlags socketFlags)
        {
            return Observable.Create<ArraySegment<byte>>(async (observer, token) =>
            {
                var buffer = new byte[size];

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var received = await socket.ReceiveAsync(buffer, 0, size, socketFlags);
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

        public static IObserver<ArraySegment<byte>> ToClientObserver(this Socket socket, int size, SocketFlags socketFlags)
        {
            return Observer.Create<ArraySegment<byte>>(async buffer =>
            {
                var sent = 0;
                while (sent < buffer.Count)
                    sent += await socket.SendAsync(buffer.Array, sent, buffer.Count - sent, socketFlags);
            });
        }
    }
}
