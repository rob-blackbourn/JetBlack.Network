using System.Collections.Generic;
using System.Net.Sockets;

namespace JetBlack.Network.RxSocketSelect.Sockets
{
    struct Checkable
    {
        public readonly List<Socket> CheckRead;
        public readonly List<Socket> CheckWrite;
        public readonly List<Socket> CheckError;

        public Checkable(List<Socket> checkRead, List<Socket> checkWrite, List<Socket> checkError)
            : this()
        {
            CheckRead = checkRead;
            CheckWrite = checkWrite;
            CheckError = checkError;
        }

        public bool IsEmpty
        {
            get { return ((CheckRead == null || CheckRead.Count == 0) && (CheckWrite == null || CheckWrite.Count == 0) && (CheckError == null || CheckError.Count == 0)); }
        }
    }
}
