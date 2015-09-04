using System;
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
        public static ISubject<DisposableValue<ArraySegment<byte>>, DisposableValue<ArraySegment<byte>>> ToFrameClientSubject(this Socket socket, SocketFlags socketFlags, BufferManager bufferManager, Selector selector, CancellationToken token)
        {
            return Subject.Create(socket.ToFrameClientObserver(socketFlags, selector, token), socket.ToFrameClientObservable(socketFlags, bufferManager, selector));
        }

        public static IObserver<DisposableValue<ArraySegment<byte>>> ToFrameClientObserver(this Socket socket, SocketFlags socketFlags, Selector selector, CancellationToken token)
        {
            return Observer.Create<DisposableValue<ArraySegment<byte>>>(disposableBuffer =>
            {
                var header = BitConverter.GetBytes(disposableBuffer.Value.Count);
                var headerState = new BufferState(header, 0, header.Length);
                var contentState = new BufferState(disposableBuffer.Value.Array, 0, disposableBuffer.Value.Count);

                try
                {
                    headerState.Advance(socket.Send(headerState.Bytes, headerState.Offset, headerState.Length, socketFlags));

                    if (headerState.Length == 0)
                        contentState.Advance(socket.Send(contentState.Bytes, contentState.Offset, contentState.Length, socketFlags));

                    if (contentState.Length == 0)
                        return;
                }
                catch (Exception exception)
                {
                    if (!exception.IsWouldBlock())
                        throw;
                }

                var waitEvent = new AutoResetEvent(false);
                var waitHandles = new[] { token.WaitHandle, waitEvent };
                Exception error = null;

                selector.AddCallback(SelectMode.SelectWrite, socket,
                    _ =>
                    {
                        try
                        {
                            if (headerState.Length > 0)
                                headerState.Advance(socket.Send(headerState.Bytes, headerState.Offset, headerState.Length, socketFlags));

                            if (headerState.Length == 0)
                                contentState.Advance(socket.Send(contentState.Bytes, contentState.Offset, contentState.Length, socketFlags));

                            if (contentState.Length == 0)
                            {
                                selector.RemoveCallback(SelectMode.SelectWrite, socket);
                                waitEvent.Set();
                            }
                        }
                        catch (Exception exception)
                        {
                            if (exception.IsWouldBlock())
                                return;

                            error = exception;
                            selector.RemoveCallback(SelectMode.SelectWrite, socket);
                            waitEvent.Set();
                        }
                    });

                while (headerState.Length > 0 && contentState.Length > 0)
                {
                    if (WaitHandle.WaitAny(waitHandles) == 0)
                        token.ThrowIfCancellationRequested();

                    if (error != null)
                        throw error;
                }

                disposableBuffer.Dispose();
            });
        }

        public static IObservable<DisposableValue<ArraySegment<byte>>> ToFrameClientObservable(this Socket socket, SocketFlags socketFlags, BufferManager bufferManager, Selector selector)
        {
            return Observable.Create<DisposableValue<ArraySegment<byte>>>(observer =>
            {
                var headerState = new BufferState(new byte[sizeof(int)], 0, sizeof(int));
                var contentState = new BufferState(null, 0, -1);

                var selectMode = socketFlags.HasFlag(SocketFlags.OutOfBand) ? SelectMode.SelectError : SelectMode.SelectRead;

                selector.AddCallback(selectMode, socket, _ =>
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
                                observer.OnNext(DisposableValue.Create(new ArraySegment<byte>(managedBuffer, 0, length), Disposable.Create(() => bufferManager.ReturnBuffer(managedBuffer))));

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
