using System;
using System.IO;
using System.Reactive.Linq;

namespace JetBlack.Examples.RxTcp.EchoClient
{
    public static class TextReaderExtensions
    {
        public static IObservable<string> ToLineObservable(this TextReader reader)
        {
            return Observable.Create<string>(async (observer, token) =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrEmpty(line))
                            break;

                        observer.OnNext(line);
                    }

                    observer.OnCompleted();
                }
                catch (Exception error)
                {
                    observer.OnError(error);
                }
            });
        }
    }
}
