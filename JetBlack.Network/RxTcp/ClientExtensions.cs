using System;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading;
using JetBlack.Network.Common;

namespace JetBlack.Network.RxTcp
{
    public static class ClientExtensions
    {
        public static ISubject<ArraySegment<byte>, ArraySegment<byte>> ToClientSubject(this TcpClient client, int size, CancellationToken token)
        {
            return Subject.Create(client.ToClientObserver(token), client.ToClientObservable(size));
        }

        public static IObservable<ArraySegment<byte>> ToClientObservable(this TcpClient client, int size)
        {
            return client.GetStream().ToStreamObservable(size);
        }

        public static IObserver<ArraySegment<byte>> ToClientObserver(this TcpClient client, CancellationToken token)
        {
            return client.GetStream().ToStreamObserver(token);
        }
    }
}
