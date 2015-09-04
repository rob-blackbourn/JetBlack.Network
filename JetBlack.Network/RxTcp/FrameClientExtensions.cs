using System;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.ServiceModel.Channels;
using System.Threading;
using JetBlack.Network.Common;

namespace JetBlack.Network.RxTcp
{
    public static class FrameClientExtensions
    {
        public static ISubject<DisposableValue<ArraySegment<byte>>, DisposableValue<ArraySegment<byte>>> ToFrameClientSubject(this TcpClient client, BufferManager bufferManager, CancellationToken token)
        {
            return Subject.Create(client.ToFrameClientObserver(token), client.ToFrameClientObservable(bufferManager));
        }

        public static IObservable<DisposableValue<ArraySegment<byte>>> ToFrameClientObservable(this TcpClient client, BufferManager bufferManager)
        {
            return client.GetStream().ToFrameStreamObservable(bufferManager);
        }

        public static IObserver<DisposableValue<ArraySegment<byte>>> ToFrameClientObserver(this TcpClient client, CancellationToken token)
        {
            return client.GetStream().ToFrameStreamObserver(token);
        }
    }
}
