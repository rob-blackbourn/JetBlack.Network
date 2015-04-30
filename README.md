JetBlack.Network
================

An experiment in using reactive extensions with network sockets.

Four versions are implemented
1.  Use Socket as the driving class with BeginReceive/EndReceive and BeginSend/EndSend.
2.  Use socket as the driving class with non-blocking sockets and select.
3.  Use Socket as the driving class with asynchronous stream methods.
4.  Use TcpListener/TcpClient classes with asynchronous stream methods.

