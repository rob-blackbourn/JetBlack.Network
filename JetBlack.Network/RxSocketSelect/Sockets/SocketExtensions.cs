using System;
using System.Net.Sockets;
using JetBlack.Network.Common;

namespace JetBlack.Network.RxSocketSelect.Sockets
{
    static class SocketExtensions
    {
        public static bool Send(this Socket socket, SocketFlags socketFlags, BufferState state)
        {
            try
            {
                state.Advance(socket.Send(state.Bytes, state.Offset, state.Length, socketFlags));
                return state.Length == 0;
            }
            catch (Exception exception)
            {
                if (exception.IsWouldBlock())
                    return false;
                throw;
            }
        }
    }
}
