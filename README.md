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

Clients read with an `IObservable<ByteBuffer>` and write with an `IObserver<ByteBuffer>` and are create by
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