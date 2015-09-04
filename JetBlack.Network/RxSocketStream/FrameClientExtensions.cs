using System;
using System.IO;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.ServiceModel.Channels;
using System.Threading;
using JetBlack.Network.Common;

namespace JetBlack.Network.RxSocketStream
{
    public static class FrameClientExtensions
    {
        public static ISubject<DisposableValue<ArraySegment<byte>>, DisposableValue<ArraySegment<byte>>> ToFrameClientSubject(this Socket socket, BufferManager bufferManager, CancellationToken token)
        {
            var stream = new NetworkStream(socket, FileAccess.ReadWrite);
            return Subject.Create(stream.ToFrameStreamObserver(token), stream.ToFrameStreamObservable(bufferManager));
        }

        public static IObservable<DisposableValue<ArraySegment<byte>>> ToFrameClientObservable(this Socket socket, BufferManager bufferManager)
        {
            return new NetworkStream(socket, FileAccess.Read).ToFrameStreamObservable(bufferManager);
        }

        public static IObserver<DisposableValue<ArraySegment<byte>>> ToFrameClientObserver(this Socket socket, CancellationToken token)
        {
            return new NetworkStream(socket, FileAccess.Write).ToFrameStreamObserver(token);
        }
    }
}
