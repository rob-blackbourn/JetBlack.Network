using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.ServiceModel.Channels;
using System.Threading;
using JetBlack.Network.Common;

namespace JetBlack.Network.RxSocketStream
{
    public static class FrameClientExtensions
    {
        public static ISubject<DisposableByteBuffer, DisposableByteBuffer> ToFrameClientSubject(this IPEndPoint endpoint, BufferManager bufferManager, CancellationToken token)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(endpoint);
            return socket.ToFrameClientSubject(bufferManager, token);
        }

        public static ISubject<DisposableByteBuffer, DisposableByteBuffer> ToFrameClientSubject(this Socket socket, BufferManager bufferManager, CancellationToken token)
        {
            var stream = new NetworkStream(socket, FileAccess.ReadWrite);
            return Subject.Create(stream.ToFrameStreamObserver(token), stream.ToFrameStreamObservable(bufferManager));
        }

        public static IObservable<DisposableByteBuffer> ToFrameClientObservable(this Socket socket, BufferManager bufferManager)
        {
            return new NetworkStream(socket, FileAccess.Read).ToFrameStreamObservable(bufferManager);
        }

        public static IObserver<DisposableByteBuffer> ToFrameClientObserver(this Socket socket, CancellationToken token)
        {
            return new NetworkStream(socket, FileAccess.Write).ToFrameStreamObserver(token);
        }
    }
}
