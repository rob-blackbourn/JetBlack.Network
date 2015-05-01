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
            return new TcpListener(endpoint).ToListenerObservable(backlog);
        }

        public static IObservable<TcpClient> ToListenerObservable(this TcpListener listener, int backlog)
        {
            return Observable.Create<TcpClient>(async (observer, token) =>
            {
                listener.Start(backlog);

                try
                {
                    while (!token.IsCancellationRequested)
                        observer.OnNext(await listener.AcceptTcpClientAsync());

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
