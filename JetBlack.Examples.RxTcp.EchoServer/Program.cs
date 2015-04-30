using System;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using JetBlack.Network.RxTcp;

namespace JetBlack.Examples.RxTcp.EchoServer
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] splitArgs = null;
            if (args.Length != 1 || (splitArgs = args[0].Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)).Length != 2)
            {
                Console.WriteLine("usage: EchoServer <hostname>:<port>");
                Console.WriteLine("example:");
                Console.WriteLine("    > EchoServer 127.0.0.1:9211");
                Environment.Exit(-1);
            }

            var endpoint = new IPEndPoint(IPAddress.Parse(splitArgs[0]), int.Parse(splitArgs[1]));

            var cts = new CancellationTokenSource();

            var listener = endpoint.ToListenerObservable(10);

            listener
                .SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe(
                    client =>
                        client.ToClientObservable(1024)
                            .SubscribeOn(TaskPoolScheduler.Default)
                            .Subscribe(client.ToClientObserver(cts.Token), cts.Token),
                    error => Console.WriteLine("Error: " + error.Message),
                    () => Console.WriteLine("OnCompleted"),
                    cts.Token);

            Console.WriteLine("Press <ENTER> to quit");
            Console.ReadLine();

            cts.Cancel();
        }
    }
}
