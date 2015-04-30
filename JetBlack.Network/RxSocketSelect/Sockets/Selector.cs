using System;
using System.Net.Sockets;
using System.Threading;
using JetBlack.Network.Common;

namespace JetBlack.Network.RxSocketSelect.Sockets
{
    public class Selector
    {
        private readonly Selectable _selectable = new Selectable();
        private readonly Socket _reader, _writer;
        private readonly byte[] _readerBuffer = new byte[1024];
        private readonly byte[] _writeBuffer = { 0 };

        public Selector()
        {
            SocketEx.MakeSocketPair(out _reader, out _writer);
            AddCallback(SelectMode.SelectRead, _reader, _ => _reader.Receive(_readerBuffer));
        }

        public void AddCallback(SelectMode mode, Socket socket, Action<Socket> callback)
        {
            _selectable.AddCallback(mode, socket, callback);

            // If we have changed the selectable sockets interup the select to wait on the new sockets.
            if (socket != _reader)
                InterruptSelect();
        }

        private void InterruptSelect()
        {
            // Sending a byte to the writer wakes up the select loop.
            _writer.Send(_writeBuffer);
        }

        public void RemoveCallback(SelectMode mode, Socket socket)
        {
            _selectable.RemoveCallback(mode, socket);
        }

        public void Start(int microSeconds, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var checkable = _selectable.CreateCheckable();

                if (checkable.IsEmpty)
                    continue;

                Socket.Select(checkable.CheckRead, checkable.CheckWrite, checkable.CheckError, microSeconds);

                // The select may have blocked for some time, so check the cancellationtoken again.
                if (token.IsCancellationRequested)
                    return;

                _selectable.InvokeCallbacks(checkable);
            }
        }
    }

}
