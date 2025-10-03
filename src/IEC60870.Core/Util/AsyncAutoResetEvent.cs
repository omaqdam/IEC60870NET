using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace IEC60870.Core.Util;

public sealed class AsyncAutoResetEvent
{
    private sealed class Waiter
    {
        public Waiter()
        {
            Tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public TaskCompletionSource<bool> Tcs { get; }
    }

    private readonly Queue<Waiter> _waiters = new();
    private bool _signaled;

    public AsyncAutoResetEvent(bool initialState = false)
    {
        _signaled = initialState;
    }

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        lock (_waiters)
        {
            if (_signaled)
            {
                _signaled = false;
                return Task.CompletedTask;
            }

            var waiter = new Waiter();
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(state => ((Waiter)state!).Tcs.TrySetCanceled(), waiter);
            }

            _waiters.Enqueue(waiter);
            return waiter.Tcs.Task;
        }
    }

    public void Set()
    {
        Waiter? toRelease = null;

        lock (_waiters)
        {
            while (_waiters.Count > 0 && toRelease is null)
            {
                var next = _waiters.Dequeue();
                if (!next.Tcs.Task.IsCanceled)
                {
                    toRelease = next;
                }
            }

            if (toRelease is null)
            {
                if (!_signaled)
                {
                    _signaled = true;
                }

                return;
            }
        }

        toRelease.Tcs.TrySetResult(true);
    }
}
