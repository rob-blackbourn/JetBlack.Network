using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using JetBlack.Network.Common;

namespace JetBlack.Network.RxSocketStream
{
    public static class ConnectExtensions
    {
        public static IObservable<Socket> ToConnectObservable(this IPEndPoint endpoint)
        {
            return Observable.Create<Socket>(async (observer, token) =>
            {
                try
                {
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    await socket.ConnectAsync(endpoint);
                    token.ThrowIfCancellationRequested();
                    observer.OnNext(socket);
                    observer.OnCompleted();
                }
                catch (Exception error)
                {
                    observer.OnError(error);
                }
            });
        }
    }
}
