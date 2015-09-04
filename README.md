# JetBlack.Network

An experiment in using reactive extensions with network sockets over TCP.

Four versions are implemented:

1.  Use [`TcpListener`](https://msdn.microsoft.com/library/system.net.sockets.tcplistener.aspx)/[`TcpClient`](https://msdn.microsoft.com/library/system.net.sockets.tcpclient.aspx) classes with asynchronous listen, connect, and stream methods.
2.  Use [`Socket`](https://msdn.microsoft.com/library/system.net.sockets.socket.aspx) as the driving class providing asynchronous listen and connect methods, but using with asynchronous stream methods.
3.  Use [`Socket`](https://msdn.microsoft.com/library/system.net.sockets.socket.aspx) as the driving class providing asynchronous methods for listen, connect, send and receive.
4.  Use [`Socket`](https://msdn.microsoft.com/library/system.net.sockets.socket.aspx) as the driving class with non-blocking sockets and a select loop.

## News

### 2015-09-04

I have folded in the changes I made while using these classes. The major change
is that the `ByteBuffer` has been replaced by `System.ArraySegment<byte>`
which has effectively the same functionality. This means I can use the methods
on `Socket` which take `IList<ArraySegment<byte>>`, and delete a class! 

The `DisposableBuffer` has been replaced by a generic wrapper class
`DisposableValue`.

I hope these changes don't screw things up for people. I think this solution is
neater, and more sympathetic to the underlying classes.

## Description

### Listening

The natural approach for a listener would be to subscribe an endpoint, and
receive clients as they connect. This is achieved by an extension method
`ToListenerObservable` which produces an observable of the form:
`IObservable<TcpClient>` or `IObservable<Socket>`. So you might do the
following:

```cs
new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9211)
    .ToListenerObservable(10)
    .Subscribe(socket => DoSomething(socket));
```

The `10` is the backlog.

### Clients

Clients read with an `IObservable<ArraySegment<byte>>` and write with an `IObserver<ArraySegment<byte>>` and are created by
extension methods which take a [`Socket`](https://msdn.microsoft.com/library/system.net.sockets.socket.aspx)
or [`TcpClient`](https://msdn.microsoft.com/library/system.net.sockets.tcpclient.aspx).
There is also an `ISubject<ArraySegment<byte>, ArraySegment<byte>>` for
reading and writing with the same object. So you might do the following:

```cs
socket.ToClientObservable(1024)
    .Subscribe(buffer => DoSomething(buffer));
```

The `ArraySegment<byte>` class has a buffer and a length (the buffer may not be full). The `1024` argument was the size
of the buffer to create. typically the extension method will also take a [`CancellationToken`](https://msdn.microsoft.com/library/system.threading.cancellationtoken.aspx) as an argument.

### Frame Clients

Frame Clients follow the same pattern to the clients, but use a [`DisposableValue<ArraySegment<byte>>`](https://github.com/rob-blackbourn/JetBlack.Network/blob/master/JetBlack.Network/Common/DisposableValue.cs)
and send/receive the length of the buffer. This ensures the full message is
received. They also take a [`BufferManager`](https://msdn.microsoft.com/library/system.servicemodel.channels.buffermanager.aspx)
to reduce garbage collection.

## Connectors

The client connection can be performed asynchronously. ClientConnectors are `IObservable<Socket>` or `IObservable<TcpClient>` and
are created by extension methods which take [`IPEndPoint`](https://msdn.microsoft.com/library/system.net.ipendpoint.aspx). So you might do the following:

```cs
new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9211)
    .ToConnectObservable()
    .Subscribe(socket => DoSomething(socket));
```
    
## Examples

For each implementation there is an example echo client and server. The following
shows the RxSocket implementation.

### Echo Server

```cs
var endpoint = ProgramArgs.Parse(args, new[] { "127.0.0.1:9211" }).EndPoint;

var cts = new CancellationTokenSource();

endpoint.ToListenerObservable(10)
    .ObserveOn(TaskPoolScheduler.Default)
    .Subscribe(
        client =>
            client.ToClientObservable(1024, SocketFlags.None)
                .Subscribe(client.ToClientObserver(1024, SocketFlags.None), cts.Token),
        error => Console.WriteLine("Error: " + error.Message),
        () => Console.WriteLine("OnCompleted"),
        cts.Token);

Console.WriteLine("Press <ENTER> to quit");
Console.ReadLine();

cts.Cancel();
```

### Echo Client

```cs
var endpoint = ProgramArgs.Parse(args, new[] { "127.0.0.1:9211" }).EndPoint;

var cts = new CancellationTokenSource();
var bufferManager = BufferManager.CreateBufferManager(2 << 16, 2 << 8);

var frameClientSubject = endpoint.ToFrameClientSubject(SocketFlags.None, bufferManager, cts.Token);

var observerDisposable =
    frameClientSubject
        .ObserveOn(TaskPoolScheduler.Default)
        .Subscribe(
            disposableBuffer =>
            {
                Console.WriteLine("Read: " + Encoding.UTF8.GetString(disposableBuffer.Value.Array, 0, disposableBuffer.Value.Count));
                disposableBuffer.Dispose();
            },
            error => Console.WriteLine("Error: " + error.Message),
            () => Console.WriteLine("OnCompleted: FrameReceiver"));

Console.In.ToLineObservable()
    .Subscribe(
        line =>
        {
            var writeBuffer = Encoding.UTF8.GetBytes(line);
            frameClientSubject.OnNext(DisposableValue.Create(new ArraySegment<byte>(writeBuffer, 0, writeBuffer.Length), Disposable.Empty));
        },
        error => Console.WriteLine("Error: " + error.Message),
        () => Console.WriteLine("OnCompleted: LineReader"));

observerDisposable.Dispose();

cts.Cancel();
```

## Implementation

### RxTcp

#### Listening

This implementation is the most straightforward. The [`TcpListener`](https://msdn.microsoft.com/library/system.net.sockets.tcplistener.aspx)
and [`TcpClient`](https://msdn.microsoft.com/library/system.net.sockets.tcpclient.aspx)
classes have asynchronous methods which can be used with `await` when
connecting and listening. The provide a
[`NetworkStream`](https://msdn.microsoft.com/library/system.net.sockets.networkstream.aspx)
which implement asynchronous methods declared by [`Stream`](https://msdn.microsoft.com/library/system.io.stream.aspx).

The listen is implemented in the following manner:

```cs
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
```

Note that the observable factory method used is the asynchonous version which
provides a cancellation token. We can use this to control exit from the listen
loop and produce the `OnCompleted` action.

#### Connecting

Connecting works in a similar manner to listening. We observe on and endpoint
and receive a client.

```cs
public static IObservable<TcpClient> ToConnectObservable(this IPEndPoint endpoint)
{
    return Observable.Create<TcpClient>(async (observer, token) =>
    {
        try
        {
            var client = new TcpClient();
            await client.ConnectAsync(endpoint.Address, endpoint.Port);
            token.ThrowIfCancellationRequested();
            observer.OnNext(client);
            observer.OnCompleted();
        }
        catch (Exception error)
        {
            observer.OnError(error);
        }
    });
}
```

As with the listener we use the asynchronous factory method. As the connect may
take some time I have added a cancellation token check after the connection
returns.

#### Reading and writing

I have implemented two readers and writers. One for bytes, and another for
"frames" which are discussed below. Note that when byte arrays are sent and
received they may be fragmented (split into separate blocks).

It is often more efficient to manage the byte arrays in a pool. When we do
this the buffers may be larger than the payload, so I use `ArraySegment<byte>`
to hold the byte array and payload length.

The clients are thin wrappers around the streams:

```cs
public static ISubject<ArraySegment<byte>, ArraySegment<byte>> ToClientSubject(this TcpClient client, int size, CancellationToken token)
{
    return Subject.Create(client.ToClientObserver(token), client.ToClientObservable(size));
}

public static IObservable<ArraySegment<byte>> ToClientObservable(this TcpClient client, int size)
{
    return client.GetStream().ToStreamObservable(size);
}

public static IObserver<ArraySegment<byte>> ToClientObserver(this TcpClient client, CancellationToken token)
{
    return client.GetStream().ToStreamObserver(token);
}
```

The stream observer (writer) is the most straightforward as the write method
guarantees to send the entire buffer.

```cs
public static IObserver<ArraySegment<byte>> ToStreamObserver(this Stream stream, CancellationToken token)
{
    return Observer.Create<ArraySegment<byte>>(async buffer =>
    {
        await stream.WriteAsync(buffer.Array, buffer.Offset, buffer.Count, token);
    });
}
```

The stream observable follows a similar pattern to the previous observables.

```cs
public static IObservable<ArraySegment<byte>> ToStreamObservable(this Stream stream, int size)
{
    return Observable.Create<ArraySegment<byte>>(async (observer, token) =>
    {
        var buffer = new byte[size];

        try
        {
            while (!token.IsCancellationRequested)
            {
                var received = await stream.ReadAsync(buffer, 0, size, token);
                if (received == 0)
                    break;

                observer.OnNext(new ArraySegment<byte>(buffer, 0, received));
            }

            observer.OnCompleted();
        }
        catch (Exception error)
        {
            observer.OnError(error);
        }
    });
}
```

I have made a decision to create a dedicated buffer for each observable. This
may not be what you want. An example using managed buffers can be seen below.

Note that the number of bytes read may be less than the size of the buffer.

The client is used in the echo server examples to read from the socket and write
it back to the client. The server doesn't need to know anything about the
message size or content so the client implementations are ideal. It simply
forwards what it receives back to the client. Here is a slightly simplified
version of the code.

```cs
endpoint.ToListenerObservable(10)
    .ObserveOn(TaskPoolScheduler.Default)
    .Subscribe(
        client =>
            client.ToClientObservable(1024)
                .Subscribe(client.ToClientObserver(cts.Token), token),
        error => Console.WriteLine("Error: " + error.Message),
        () => Console.WriteLine("OnCompleted"),
        token);
```

Note how we can use the rx `ObserveOn` method to handle the client thread
creation.

The frame clients manage fragmentation by sending/receiving the length of the
byte array, before sending the array itself. Because what is read or written is
now of indeterminate length I use managed buffers. With managed buffers there
must be a mechanism to return the buffer to the pool. To achieve this we use a
disposable wrapper around the buffer.

```cs
// Factory
public static class DisposableValue
{
    public static DisposableValue<T> Create<T>(T value, IDisposable disposable)
    {
        return new DisposableValue<T>(value, disposable);
    }
}

public struct DisposableValue<T> : IDisposable, IEquatable<DisposableValue<T>>
{
    private IDisposable _disposable;

    public static readonly DisposableValue<T> Empty;
        
    public DisposableValue(T value, IDisposable disposable) : this()
    {
        Value = value;
        _disposable = disposable;
    }

    public T Value { get; private set; }

    public override int GetHashCode()
    {
        return Equals(Value, default(T)) ? 0 : Value.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        return obj is DisposableValue<T> && Equals((DisposableValue<T>)obj);
    }

    public bool Equals(DisposableValue<T> other)
    {
        return Equals(Value, other.Value) && Equals(_disposable, other._disposable);
    }

    public static bool operator ==(DisposableValue<T> a, DisposableValue<T> b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(DisposableValue<T> a, DisposableValue<T> b)
    {
        return !a.Equals(b);
    }

    public void Dispose()
    {
        IDisposable disposable = Interlocked.CompareExchange<IDisposable>(ref _disposable, null, _disposable);
        if (disposable != null)
            disposable.Dispose();
    }
}
```

The frame clients simply delegate the behaviour to their streams.

```cs
public static ISubject<DisposableValue<ArraySegment<byte>>, DisposableValue<ArraySegment<byte>>> ToFrameClientSubject(this TcpClient client, BufferManager bufferManager, CancellationToken token)
{
    return Subject.Create(client.ToFrameClientObserver(token), client.ToFrameClientObservable(bufferManager));
}

public static IObservable<DisposableValue<ArraySegment<byte>>> ToFrameClientObservable(this TcpClient client, BufferManager bufferManager)
{
    return client.GetStream().ToFrameStreamObservable(bufferManager);
}

public static IObserver<DisposableValue<ArraySegment<byte>>> ToFrameClientObserver(this TcpClient client, CancellationToken token)
{
    return client.GetStream().ToFrameStreamObserver(token);
}
```

The observer is straightforward.

```cs
public static IObserver<DisposableValue<ArraySegment<byte>>> ToFrameStreamObserver(this Stream stream, CancellationToken token)
{
    return Observer.Create<DisposableValue<ArraySegment<byte>>>(async disposableBuffer =>
    {
        var headerBuffer = BitConverter.GetBytes(disposableBuffer.Value.Count);
        await stream.WriteAsync(headerBuffer, 0, headerBuffer.Length, token);
        await stream.WriteAsync(disposableBuffer.Value.Array, 0, disposableBuffer.Value.Count, token);
    });
}
```

We use the [`BitConverter`](https://msdn.microsoft.com/library/system.bitconverter.aspx) to turn the length into a byte stream and send it as
the first packet. Finally the byte array is sent.

The observable requires a helper method to ensure all the required bytes are read.

```cs
public static async Task<int> ReadBytesCompletelyAsync(this Stream stream, byte[] buf, int length, CancellationToken token)
{
    var read = 0;
    while (read < length)
    {
        var remaining = length - read;
        var bytes = await stream.ReadAsync(buf, read, remaining, token);
        if (bytes == 0)
            return read;

        read += bytes;
    }
    return read;
}
```

We need to handle the case where no bytes are returned because the socket is
closed. We could throw an exception, but I prefer not to use exceptions to
control logic, so I return the actual length read.

Finally the frame stream observable.

```cs
public static IObservable<DisposableValue<ArraySegment<byte>>> ToFrameStreamObservable(this Stream stream, BufferManager bufferManager)
{
    return Observable.Create<DisposableValue<ArraySegment<byte>>>(async (observer, token) =>
    {
        var headerBuffer = new byte[sizeof(int)];

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (await stream.ReadBytesCompletelyAsync(headerBuffer, headerBuffer.Length, token) != headerBuffer.Length)
                    break;
                var length = BitConverter.ToInt32(headerBuffer, 0);

                var buffer = bufferManager.TakeBuffer(length);
                if (await stream.ReadBytesCompletelyAsync(buffer, length, token) != length)
                    break;

                observer.OnNext(DisposableValue.Create(new ArraySegment<byte>(buffer, 0, length), Disposable.Create(() => bufferManager.ReturnBuffer(buffer))));
            }

            observer.OnCompleted();
        }
        catch (Exception error)
        {
            observer.OnError(error);
        }
    });
}
```

I choose not to use the buffer manager to allocate the header buffer as it is
only four bytes. We check the actual number of bytes read to detect closed
sockets, then decode the length with [`BitConverter`](https://msdn.microsoft.com/library/system.bitconverter.aspx).

Once the length of the content is known we use 
[`BufferManager`](https://msdn.microsoft.com/library/system.servicemodel.channels.buffermanager.aspx) to provide the byte array.
The disposable buffer is primed to return the buffer when `Dispose` is called.

The following example shows how the buffer is finally disposed by the echo
client.

```cs
var observerDisposable =
    ToFrameClientObserver(client, bufferManager)
        .ObserveOn(TaskPoolScheduler.Default)
        .Subscribe(
            disposableBuffer =>
            {
                Console.WriteLine("Read: " + Encoding.UTF8.GetString(disposableBuffer.Value.Array, 0, disposableBuffer.Value.Count));
                disposableBuffer.Dispose();
            },
            error => Console.WriteLine("Error: " + error.Message),
            () => Console.WriteLine("OnCompleted: FrameReceiver"));
```

### RxSocketStream

This is almost as trivial as RxTcp as it uses the `Stream` based asynchronous
methods for reading and writing. However it does need to implement the
asynchronous task pattern for listen and connect.

```cs
public static async Task<Socket> AcceptAsync(this Socket socket)
{
    return await Task<Socket>.Factory.FromAsync(socket.BeginAccept, socket.EndAccept, null);
}

public static async Task ConnectAsync(this Socket socket, IPEndPoint endpoint)
{
    await Task.Factory.FromAsync((callback, state) => socket.BeginConnect(endpoint, callback, state), ias => socket.EndConnect(ias), null);
}
```

For some reason this does not work if the `EndXXX` call is a method group.

### RxSocket

This follows on from RxSocketStream by using asynchronous patterns for the
sending and receiving. I have added a parameter for [`SocketFlags`](https://msdn.microsoft.com/library/system.net.sockets.socketflags.aspx). We should
be able to send and receive out of band, but I have not tried this.

### RxSocketSelect

This is the most convoluted implementation as it uses [`Socket.Select`](https://msdn.microsoft.com/en-us/library/system.net.sockets.socket.select(v=vs.110).aspx) to
provide the asynchronous behaviour. I implemented this as a challenge! but also
to find out what the difference in performance was on Unix based systems using
Mono.

The implementation is fairly complete, but I have not tried the out of band reading.

## Wrap Up

Please let me know if you find any problems and I'll apply the fixes.

Rob