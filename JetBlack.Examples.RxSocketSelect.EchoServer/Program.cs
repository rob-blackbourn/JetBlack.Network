using System;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBlack.Examples.Common;
using JetBlack.Network.RxSocketSelect;
using JetBlack.Network.RxSocketSelect.Sockets;

namespace JetBlack.Examples.RxSocketSelect.EchoServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var endpoint = ProgramArgs.Parse(args, new[] { "127.0.0.1:9211" }).EndPoint;

            var cts = new CancellationTokenSource();
            var selector = new Selector();

            var listener = endpoint.ToListenerObservable(10, selector);

            listener
                .SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe(
                    client =>
                        client.ToClientObservable(1024, SocketFlags.None, selector)
                            .SubscribeOn(TaskPoolScheduler.Default)
                            .Subscribe(client.ToClientObserver(SocketFlags.None, selector, cts.Token), cts.Token),
                    error => Console.WriteLine("Error: " + error.Message),
                    () => Console.WriteLine("OnCompleted"),
                    cts.Token);

            Task.Factory.StartNew(() => selector.Start(60000000, cts.Token), cts.Token);

            Console.WriteLine("Press <ENTER> to quit");
            Console.ReadLine();

            cts.Cancel();
        }
    }
}
