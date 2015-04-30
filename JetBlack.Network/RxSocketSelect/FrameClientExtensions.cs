using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.ServiceModel.Channels;
using System.Threading;
using JetBlack.Network.Common;
using JetBlack.Network.RxSocketSelect.Sockets;

namespace JetBlack.Network.RxSocketSelect
{
    public static class FrameClientExtensions
    {
        public static ISubject<DisposableByteBuffer, DisposableByteBuffer> ToFrameClientSubject(this IPEndPoint endpoint, SocketFlags socketFlags, BufferManager bufferManager, Selector selector, CancellationToken token)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(endpoint);
            socket.Blocking = false;
            return socket.ToFrameClientSubject(socketFlags, bufferManager, selector, token);
        }

        public static ISubject<DisposableByteBuffer, DisposableByteBuffer> ToFrameClientSubject(this Socket socket, SocketFlags socketFlags, BufferManager bufferManager, Selector selector, CancellationToken token)
        {
            return Subject.Create(socket.ToFrameClientObserver(socketFlags, selector, token), socket.ToFrameClientObservable(socketFlags, bufferManager, selector));
        }

        public static IObserver<DisposableByteBuffer> ToFrameClientObserver(this Socket socket, SocketFlags socketFlags, Selector selector, CancellationToken token)
        {
            return Observer.Create<DisposableByteBuffer>(disposableBuffer =>
            {
                var header = BitConverter.GetBytes(disposableBuffer.Length);
                var headerState = new BufferState(header, 0, header.Length);

                var contentState = new BufferState(disposableBuffer.Bytes, 0, disposableBuffer.Length);

                if (socket.Poll(0, SelectMode.SelectWrite) && socket.Send(socketFlags, headerState))
                    if (socket.Poll(0, SelectMode.SelectWrite) && socket.Send(socketFlags, contentState))
                    {
                        disposableBuffer.Dispose();
                        return;
                    }

                var waitEvent = new AutoResetEvent(false);
                var waitHandles = new[] { token.WaitHandle, waitEvent };

                selector.AddCallback(SelectMode.SelectWrite, socket,
                    _ =>
                    {
                        try
                        {
                            if (headerState.Length > 0)
                                socket.Send(socketFlags, headerState);

                            if (headerState.Length == 0 && socket.Send(socketFlags, contentState))
                                selector.RemoveCallback(SelectMode.SelectWrite, socket);
                        }
                        finally
                        {
                            waitEvent.Set();
                        }
                    });

                while (headerState.Length > 0 && contentState.Length > 0)
                {
                    if (WaitHandle.WaitAny(waitHandles) == 0)
                        token.ThrowIfCancellationRequested();
                }

                disposableBuffer.Dispose();
            });
        }

        public static IObservable<DisposableByteBuffer> ToFrameClientObservable(this Socket socket, SocketFlags socketFlags, BufferManager bufferManager, Selector selector)
        {
            return Observable.Create<DisposableByteBuffer>(observer =>
            {
                var headerState = new BufferState(new byte[sizeof(int)], 0, sizeof(int));
                var contentState = new BufferState(null, 0, -1);

                selector.AddCallback(SelectMode.SelectRead, socket, _ =>
                {
                    try
                    {
                        if (headerState.Length > 0)
                        {
                            headerState.Advance(socket.Receive(headerState.Bytes, headerState.Offset, headerState.Length, socketFlags));

                            if (headerState.Length == 0)
                            {
                                contentState.Length = BitConverter.ToInt32(headerState.Bytes, 0);
                                contentState.Offset = 0;
                                contentState.Bytes = bufferManager.TakeBuffer(contentState.Length);
                            }
                        }

                        if (contentState.Bytes != null)
                        {
                            contentState.Advance(socket.Receive(contentState.Bytes, contentState.Offset, contentState.Length, socketFlags));

                            if (contentState.Length == 0)
                            {
                                var managedBuffer = contentState.Bytes;
                                var length = contentState.Offset;
                                observer.OnNext(new DisposableByteBuffer(managedBuffer, length, Disposable.Create(() => bufferManager.ReturnBuffer(managedBuffer))));

                                contentState.Bytes = null;

                                headerState.Length = headerState.Offset;
                                headerState.Offset = 0;
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        if (!exception.IsWouldBlock())
                            observer.OnError(exception);
                    }
                });

                return Disposable.Create(() => selector.RemoveCallback(SelectMode.SelectRead, socket));
            });
        }
    }
}
