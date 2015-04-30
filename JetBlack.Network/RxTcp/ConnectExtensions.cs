using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;

namespace JetBlack.Network.RxTcp
{
    public static class ConnectExtensions
    {
        public static IObservable<TcpClient> ToConnectObservable(this IPEndPoint endpoint)
        {
            return Observable.Create<TcpClient>(async (observer, token) =>
            {
                var client = new TcpClient();
                await client.ConnectAsync(endpoint.Address, endpoint.Port);
                token.ThrowIfCancellationRequested();
                observer.OnNext(client);
            });
        }
    }
}
