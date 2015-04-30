using System;
using System.Net;

namespace JetBlack.Examples.Common
{
    public class ProgramArgs
    {
        public ProgramArgs(IPEndPoint endPoint)
        {
            EndPoint = endPoint;
        }

        public IPEndPoint EndPoint { get; private set; }

        public static ProgramArgs Parse(string[] args, string[] defaultArgs)
        {
            if (args.Length == 0) args = defaultArgs;

            string[] splitArgs = null;
            if (args.Length != 1 || (splitArgs = args[0].Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)).Length != 2)
            {
                Console.WriteLine("usage: EchoClient <address>:<port>");
                Console.WriteLine("example:");
                Console.WriteLine("    > EchoClient 127.0.0.1:9211");
                Environment.Exit(-1);
            }

            return new ProgramArgs(new IPEndPoint(IPAddress.Parse(splitArgs[0]), int.Parse(splitArgs[1])));
        }
    }
}
