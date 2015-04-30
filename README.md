# JetBlack.Network

An experiment in using reactive extensions with network sockets.

Four versions are implemented:

1.  Use Socket as the driving class with BeginReceive/EndReceive and BeginSend/EndSend.
2.  Use socket as the driving class with non-blocking sockets and select.
3.  Use Socket as the driving class with asynchronous stream methods.
4.  Use TcpListener/TcpClient classes with asynchronous stream methods.

## Description

### Listeners

Listeners are `IObservable<TcpClient>` or `IObservable<Socket>` and are created by extension methods which
take and `IPEndPoint`. So you might do the following:

    new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9211)
        .ToListenerObservable(10)
        .Subscribe(socket => DoSomething(socket));

The `10` is the backlog.

### Clients

Clients read with an `IObservable<ByteBuffer>` and write with an `IObserver<ByteBuffer>` and are created by
extension methods which take a `Socket` or `TcpClient`. There is also an `ISubject<ByteBuffer,ByteBuffer>` for
reading and writing with the same object. So you might do the following:

    socket.ToClientObservable(1024)
        .Subscribe(buffer => DoSomething(buffer));

The `ByteBuffer` class has a buffer and a length (the buffer may not be full). The `1024` argument was the size
of the buffer to create. typically the extension method will also take a `CancellationToken` as an argument.

### Frame Clients

Frame Clients follow the same pattern to the clients, but use a `DispoableByteBuffer` and send/receive the length
of the buffer. This ensures the full message is received. They also take a `BufferManager` to reduce garbage collection.

## Examples

For each implementation there is an example echo client and server.

For the RxSocket implementation the server looks like this:

    var endpoint = ProgramArgs.Parse(args, new[] { "127.0.0.1:9211" }).EndPoint;

    var cts = new CancellationTokenSource();

    var listener = endpoint.ToListenerObservable(10);

    listener
        .SubscribeOn(TaskPoolScheduler.Default)
        .Subscribe(
            client =>
                client.ToClientObservable(1024, SocketFlags.None)
                    .SubscribeOn(TaskPoolScheduler.Default)
                    .Subscribe(client.ToClientObserver(1024, SocketFlags.None), cts.Token),
            error => Console.WriteLine("Error: " + error.Message),
            () => Console.WriteLine("OnCompleted"),
            cts.Token);

    Console.WriteLine("Press <ENTER> to quit");
    Console.ReadLine();

    cts.Cancel();

And the client looks like this:

    var endpoint = ProgramArgs.Parse(args, new[] { "127.0.0.1:9211" }).EndPoint;

    var cts = new CancellationTokenSource();
    var bufferManager = BufferManager.CreateBufferManager(2 << 16, 2 << 8);

    var frameClientSubject = endpoint.ToFrameClientSubject(SocketFlags.None, bufferManager, cts.Token);

    var observerDisposable =
        frameClientSubject
            .SubscribeOn(TaskPoolScheduler.Default)
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

Please let me know if you find any problems and I'll apply the fixes.

Rob