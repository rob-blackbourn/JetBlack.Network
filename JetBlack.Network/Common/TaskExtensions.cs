using System;
using System.Threading;
using System.Threading.Tasks;

namespace JetBlack.Network.Common
{
    public static class TaskExtensions
    {
        public static async Task<T> WithCancellableWait<T>(
            this Task<T> task,
            CancellationToken token)
        {
            var cancellation = new TaskCompletionSource<bool>();
            using (token.Register(s =>
                 (s as TaskCompletionSource<bool>).TrySetResult(true), cancellation))
            {
                if (task != await Task.WhenAny(task, cancellation.Task))
                    throw new OperationCanceledException(token);
                return await task;
            }
        }
    }
}