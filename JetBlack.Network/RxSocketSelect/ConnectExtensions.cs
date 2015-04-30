using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using JetBlack.Network.Common;
using JetBlack.Network.RxSocketSelect.Sockets;

namespace JetBlack.Network.RxSocketSelect
{
    public static class ConnectExtensions
    {
        public static IObservable<Socket> ToConnectObservable(this IPEndPoint endpoint, Selector selector, CancellationToken token)
        {
            return Observable.Create<Socket>(observer =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { Blocking = false };

                try
                {
                    socket.Connect(endpoint);
                    observer.OnNext(socket);
                }
                catch (Exception exception)
                {
                    if (!exception.IsWouldBlock())
                        observer.OnError(exception);
                }

                var waitEvent = new ManualResetEvent(false);
                var waitHandles = new[] { token.WaitHandle, waitEvent };
                Exception error = null;

                selector.AddCallback(SelectMode.SelectWrite, socket,
                    _ =>
                    {
                        try
                        {
                            if (!socket.Connected)
                                socket.Connect(endpoint);
                            selector.RemoveCallback(SelectMode.SelectWrite, socket);
                            waitEvent.Set();
                        }
                        catch (Exception exception)
                        {
                            if (exception.IsWouldBlock())
                                return;
                            error = exception;
                            waitEvent.Set();
                        }
                    });

                if (WaitHandle.WaitAny(waitHandles) == 0)
                    token.ThrowIfCancellationRequested();

                if (error == null)
                    observer.OnNext(socket);
                else
                    observer.OnError(error);

                return Disposable.Empty;
            });
        }
    }
}
