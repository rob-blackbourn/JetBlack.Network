# JetBlack.Network

An experiment in using reactive extensions with network sockets over TCP.

Four versions are implemented:

1.  Use TcpListener/TcpClient classes with asynchronous listen, connect, and stream methods.
2.  Use Socket as the driving class providing asynchronous listen and connect methods, but using with asynchronous stream methods.
3.  Use Socket as the driving class providing asynchronous methods for listen, connect, send and receive.
4.  Use socket as the driving class with non-blocking sockets and a select loop.

## Description

### Listening

The natural approach for a listener would be to subscribe an endpoint, and
receive clients as they connect. This is achieved by an extension method
`ToListenerObservable` which produces an observable of the form:
`IObservable<TcpClient>` or `IObservable<Socket>`. So you might do the
following:

    new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9211)
        .ToListenerObservable(10)
        .Subscribe(socket => DoSomething(socket));

The `10` is the backlog.

### Clients

Clients read with an `IObservable<ByteBuffer>` and write with an `IObserver<ByteBuffer>` and are created by
extension methods which take a `Socket` or `TcpClient`. There is also an `ISubject<ByteBuffer, ByteBuffer>` for
reading and writing with the same object. So you might do the following:

    socket.ToClientObservable(1024)
        .Subscribe(buffer => DoSomething(buffer));

The `ByteBuffer` class has a buffer and a length (the buffer may not be full). The `1024` argument was the size
of the buffer to create. typically the extension method will also take a `CancellationToken` as an argument.

### Frame Clients

Frame Clients follow the same pattern to the clients, but use a `DispoableByteBuffer` and send/receive the length
of the buffer. This ensures the full message is received. They also take a `BufferManager` to reduce garbage collection.

## Connectors

The client connection can be performed asynchronously. ClientConnectors are `IObservable<Socket>` or `IObservable<TcpClient>` and
are created by extension methods which take `IPEndPoint`. So you might do the following:

    new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9211)
        .ToConnectObservable()
        .Subscribe(socket => DoSomething(socket));
    
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
                    .ObserveOn(TaskPoolScheduler.Default)
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

## Implementation

### RxTcp

#### Listening

This implementation is the most straightforward. The `TcpListener` and `TcpClient` classes have
asynchronous methods which can be used with `await` when connecting and listening. The provide
a `NetworkStream` which inherits asynchronous methods from `Stream`.

The listen is implemented in the following manner:

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

#### Connecting

Connect is more trivial:

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

#### Reading and writing

The clients are thin wrappers around the streams:

    public static ISubject<ByteBuffer, ByteBuffer> ToClientSubject(this TcpClient client, int size, CancellationToken token)
    {
        return Subject.Create(client.ToClientObserver(token), client.ToClientObservable(size));
    }

    public static IObservable<ByteBuffer> ToClientObservable(this TcpClient client, int size)
    {
        return client.GetStream().ToStreamObservable(size);
    }

    public static IObserver<ByteBuffer> ToClientObserver(this TcpClient client, CancellationToken token)
    {
        return client.GetStream().ToStreamObserver(token);
    }

The streams follow a simple pattern:

    public static IObservable<ByteBuffer> ToStreamObservable(this Stream stream, int size)
    {
        return Observable.Create<ByteBuffer>(async (observer, token) =>
        {
            var buffer = new byte[size];

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var received = await stream.ReadAsync(buffer, 0, size, token);
                    if (received == 0)
                        break;

                    observer.OnNext(new ByteBuffer(buffer, received));
                }

                observer.OnCompleted();
            }
            catch (Exception error)
            {
                observer.OnError(error);
            }
        });
    }

    public static IObserver<ByteBuffer> ToStreamObserver(this Stream stream, CancellationToken token)
    {
        return Observer.Create<ByteBuffer>(async buffer =>
        {
            await stream.WriteAsync(buffer.Bytes, 0, buffer.Length, token);
        });
    }

### RxSocketStream

This is almost as trivial as RxTcp as it uses the `Stream` based asynchornous methods for
reading and writing. However it does need to implement the asynchronous `Task` pattern for listeneing and connect.

    public static async Task<Socket> AcceptAsync(this Socket socket)
    {
        return await Task<Socket>.Factory.FromAsync(socket.BeginAccept, socket.EndAccept, null);
    }

### RxSocket

This follows on from RxSocketStream by using asynchronous patterns for the sending and receiving.

### RxSocketSelect

This is the most convoluted implementation as it uses `Socket.Select` to provide the asynchronous behaviour.

I implemented this as a challenge! but also to find out what the difference in performance was on Unix based systems using Mono.

## Wrap Up

Please let me know if you find any problems and I'll apply the fixes.

Rob