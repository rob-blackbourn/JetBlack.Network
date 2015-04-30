using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;

namespace JetBlack.Network.RxTcp
{
    public static class ListenerExtensions
    {
        public static IObservable<TcpClient> ToListenerObservable(this IPEndPoint endpoint, int backlog)
        {
            var listener = new TcpListener(endpoint);
            return listener.ToListenerObservable(10);
        }

        public static IObservable<TcpClient> ToListenerObservable(this TcpListener listener, int backlog)
        {
            return Observable.Create<TcpClient>(async (observer, token) =>
            {
                listener.Start(backlog);

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var client = await listener.AcceptTcpClientAsync();
                        if (client == null)
                            break;

                        observer.OnNext(client);
                    }

                    observer.OnCompleted();

                    listener.Stop();
                }
                catch (Exception error)
                {
                    observer.OnError(error);
                }
            });
        }
    }
}
