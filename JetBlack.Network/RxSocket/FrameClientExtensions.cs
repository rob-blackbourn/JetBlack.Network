using System;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.ServiceModel.Channels;
using System.Threading;
using JetBlack.Network.Common;

namespace JetBlack.Network.RxSocket
{
    public static class FrameClientExtensions
    {
        public static ISubject<DisposableValue<ArraySegment<byte>>, DisposableValue<ArraySegment<byte>>> ToFrameClientSubject(this Socket socket, SocketFlags socketFlags, BufferManager bufferManager, CancellationToken token)
        {
            return Subject.Create(socket.ToFrameClientObserver(socketFlags, token), socket.ToFrameClientObservable(socketFlags, bufferManager));
        }

        public static IObservable<DisposableValue<ArraySegment<byte>>> ToFrameClientObservable(this Socket socket, SocketFlags socketFlags, BufferManager bufferManager)
        {
            return Observable.Create<DisposableValue<ArraySegment<byte>>>(async (observer, token) =>
            {
                var headerBuffer = new byte[sizeof(int)];

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (await socket.ReceiveCompletelyAsync(headerBuffer, headerBuffer.Length, socketFlags, token) != headerBuffer.Length)
                            break;
                        var length = BitConverter.ToInt32(headerBuffer, 0);

                        var buffer = bufferManager.TakeBuffer(length);
                        if (await socket.ReceiveCompletelyAsync(buffer, length, socketFlags, token) != length)
                            break;

                        observer.OnNext(
                            new DisposableValue<ArraySegment<byte>>(new ArraySegment<byte>(buffer, 0, length),
                                Disposable.Create(() => bufferManager.ReturnBuffer(buffer))));
                    }

                    observer.OnCompleted();

                    socket.Close();
                }
                catch (Exception error)
                {
                    observer.OnError(error);
                }
            });
        }

        public static IObserver<DisposableValue<ArraySegment<byte>>> ToFrameClientObserver(this Socket socket, SocketFlags socketFlags, CancellationToken token)
        {
            return Observer.Create<DisposableValue<ArraySegment<byte>>>(async disposableBuffer =>
            {
                var headerBuffer = BitConverter.GetBytes(disposableBuffer.Value.Count);
                await socket.SendCompletelyAsync(
                    new[]
                    {
                        new ArraySegment<byte>(headerBuffer, 0, headerBuffer.Length),
                        new ArraySegment<byte>(disposableBuffer.Value.Array, 0, disposableBuffer.Value.Count)
                    },
                    SocketFlags.None,
                    token);
            });
        }
    }
}
