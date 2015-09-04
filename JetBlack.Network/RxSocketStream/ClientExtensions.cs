using System;
using System.IO;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading;
using JetBlack.Network.Common;

namespace JetBlack.Network.RxSocketStream
{
    public static class ClientExtensions
    {
        public static ISubject<ArraySegment<byte>, ArraySegment<byte>> ToClientSubject(this Socket socket, int size, CancellationToken token)
        {
            var stream = new NetworkStream(socket, FileAccess.ReadWrite);
            return Subject.Create(stream.ToStreamObserver(token), stream.ToStreamObservable(size));
        }

        public static IObservable<ArraySegment<byte>> ToClientObservable(this Socket socket, int size)
        {
            return new NetworkStream(socket, FileAccess.Read).ToStreamObservable(size);
        }

        public static IObserver<ArraySegment<byte>> ToClientObserver(this Socket socket, CancellationToken token)
        {
            return new NetworkStream(socket, FileAccess.Write).ToStreamObserver(token);
        }
    }
}
