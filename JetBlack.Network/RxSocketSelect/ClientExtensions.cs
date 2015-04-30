using System;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using JetBlack.Network.Common;
using JetBlack.Network.RxSocketSelect.Sockets;

namespace JetBlack.Network.RxSocketSelect
{
    public static class ClientExtensions
    {
        public static IObservable<ByteBuffer> ToClientObservable(this Socket socket, int size, SocketFlags socketFlags, Selector selector)
        {
            return Observable.Create<ByteBuffer>(observer =>
            {
                var buffer = new byte[size];

                selector.AddCallback(SelectMode.SelectRead, socket, _ =>
                {
                    try
                    {
                        var bytes = socket.Receive(buffer, 0, size, socketFlags);
                        if (bytes == 0)
                            observer.OnCompleted();
                        else
                            observer.OnNext(new ByteBuffer(buffer, bytes));
                    }
                    catch (Exception error)
                    {
                        if (error.IsWouldBlock())
                            return;
                        observer.OnError(error);
                    }
                });

                return Disposable.Create(() => selector.RemoveCallback(SelectMode.SelectRead, socket));
            });
        }

        public static IObserver<ByteBuffer> ToClientObserver(this Socket socket, SocketFlags socketFlags, Selector selector, CancellationToken token)
        {
            return Observer.Create<ByteBuffer>(
                buffer =>
                {
                    var state = new BufferState(buffer.Bytes, 0, buffer.Length);

                    // Try to write as much as possible without registering a callback.
                    try
                    {
                        state.Advance(socket.Send(state.Bytes, state.Offset, state.Length, socketFlags));
                        if (state.Length == 0)
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
                                state.Advance(socket.Send(state.Bytes, state.Offset, state.Length, socketFlags));
                                if (state.Length == 0)
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

                    while (state.Length > 0)
                    {
                        if (WaitHandle.WaitAny(waitHandles) == 0)
                            token.ThrowIfCancellationRequested();
                        
                        if (error != null)
                            throw error;
                    }
                });
        }
    }
}
