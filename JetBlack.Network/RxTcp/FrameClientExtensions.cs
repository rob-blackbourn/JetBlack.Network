using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.ServiceModel.Channels;
using System.Threading;
using JetBlack.Network.Common;

namespace JetBlack.Network.RxTcp
{
    public static class FrameClientExtensions
    {
        public static IObservable<TcpClient> ToConnectObservable(this IPEndPoint endpoint)
        {
            return Observable.Create<TcpClient>(async (observer, token) =>
            {
                var client = new TcpClient();
                await client.ConnectAsync(endpoint.Address, endpoint.Port);
                token.ThrowIfCancellationRequested();
                observer.OnNext(client);
            });
        }

        public static ISubject<DisposableByteBuffer, DisposableByteBuffer> ToFrameClientSubject(this TcpClient client, BufferManager bufferManager, CancellationToken token)
        {
            return Subject.Create(client.ToFrameClientObserver(token), client.ToFrameClientObservable(bufferManager));
        }

        public static IObservable<DisposableByteBuffer> ToFrameClientObservable(this TcpClient client, BufferManager bufferManager)
        {
            return client.GetStream().ToFrameStreamObservable(bufferManager);
        }

        public static IObserver<DisposableByteBuffer> ToFrameClientObserver(this TcpClient client, CancellationToken token)
        {
            return client.GetStream().ToFrameStreamObserver(token);
        }
    }
}
