using System;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading;
using JetBlack.Network.Common;

namespace JetBlack.Network.RxTcp
{
    public static class ClientExtensions
    {
        public static ISubject<ByteBuffer, ByteBuffer> ToClientSubject(this TcpClient client, int size, CancellationToken token)
        {
            return Subject.Create(client.ToClientObserver(token), client.ToClientObservable(size));
        }

        public static IObservable<ByteBuffer> ToClientObservable(this TcpClient client, int size)
        {
            return client.GetStream().ToStreamObservable(size);
        }

        public static IObserver<ByteBuffer> ToClientObserver(this TcpClient client, CancellationToken token)
        {
            return client.GetStream().ToStreamObserver(token);
        }
    }
}
