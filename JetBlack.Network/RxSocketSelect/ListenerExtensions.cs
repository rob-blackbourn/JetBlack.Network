using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using JetBlack.Network.RxSocketSelect.Sockets;

namespace JetBlack.Network.RxSocketSelect
{
    public static class ListenerExtensions
    {
        public static IObservable<Socket> ToListenerObservable(this IPEndPoint endpoint, int backlog, Selector selector)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { Blocking = true };
            socket.Bind(endpoint);
            return socket.ToListenerObservable(10, selector);
        }

        public static IObservable<Socket> ToListenerObservable(this Socket socket, int backlog, Selector selector)
        {
            return Observable.Create<Socket>(observer =>
            {
                socket.Listen(backlog);

                selector.AddCallback(SelectMode.SelectRead, socket, _ =>
                {
                    var accepted = socket.Accept();
                    accepted.Blocking = false;
                    observer.OnNext(accepted);
                });

                return Disposable.Create(() => selector.RemoveCallback(SelectMode.SelectRead, socket));
            });
        }
    }
}
