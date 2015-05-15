using System;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using JetBlack.Examples.Common;
using JetBlack.Network.Common;
using JetBlack.Network.RxSocket;

namespace JetBlack.Examples.RxSocket.EchoClient
{
    class Program
    {
        public static void Main(string[] args)
        {
            var endpoint = ProgramArgs.Parse(args, new[] { "127.0.0.1:9211" }).EndPoint;

            var cts = new CancellationTokenSource();
            var bufferManager = BufferManager.CreateBufferManager(2 << 16, 2 << 8);

            endpoint.ToConnectObservable()
                .ObserveOn(TaskPoolScheduler.Default)
                .Subscribe(socket =>
                {
                    var frameClientSubject = socket.ToFrameClientSubject(SocketFlags.None, bufferManager, cts.Token);

                    var observerDisposable =
                        frameClientSubject
                            .ObserveOn(TaskPoolScheduler.Default)
                            .Subscribe(
                                managedBuffer =>
                                {
                                    Console.WriteLine("Read: " + Encoding.UTF8.GetString(managedBuffer.Bytes, 0, managedBuffer.Length));
                                    managedBuffer.Dispose();
                                },
                                error => Console.WriteLine("Error: " + error.Message),
                                () => Console.WriteLine("OnCompleted: FrameReceiver"));

                    Console.In.ToLineObservable()
                        .Subscribe(
                            line =>
                            {
                                var writeBuffer = Encoding.UTF8.GetBytes(line);
                                frameClientSubject.OnNext(new DisposableByteBuffer(writeBuffer, writeBuffer.Length, Disposable.Empty));
                            },
                            error => Console.WriteLine("Error: " + error.Message),
                            () => Console.WriteLine("OnCompleted: LineReader"));

                    observerDisposable.Dispose();

                    cts.Cancel();
                }, 
                error => Console.WriteLine("Failed to connect: " + error.Message),
                cts.Token);

            cts.Token.WaitHandle.WaitOne();
        }
    }
}
