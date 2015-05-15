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

        public static ISubject<DisposableByteBuffer, DisposableByteBuffer> ToFrameClientSubject(this Socket socket, SocketFlags socketFlags, BufferManager bufferManager, CancellationToken token)
        {
            return Subject.Create(socket.ToFrameClientObserver(socketFlags, token), socket.ToFrameClientObservable(socketFlags, bufferManager));
        }

        public static IObservable<DisposableByteBuffer> ToFrameClientObservable(this Socket socket, SocketFlags socketFlags, BufferManager bufferManager)
        {
            return Observable.Create<DisposableByteBuffer>(async (observer, token) =>
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

                        observer.OnNext(new DisposableByteBuffer(buffer, length, Disposable.Create(() => bufferManager.ReturnBuffer(buffer))));
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

        public static IObserver<DisposableByteBuffer> ToFrameClientObserver(this Socket socket, SocketFlags socketFlags, CancellationToken token)
        {
            return Observer.Create<DisposableByteBuffer>(async managedBuffer =>
            {
                var headerBuffer = BitConverter.GetBytes(managedBuffer.Length);
                await socket.SendCompletelyAsync(headerBuffer, headerBuffer.Length, socketFlags, token);
                await socket.SendCompletelyAsync(managedBuffer.Bytes, managedBuffer.Length, socketFlags, token);
            });
        }
    }
}
