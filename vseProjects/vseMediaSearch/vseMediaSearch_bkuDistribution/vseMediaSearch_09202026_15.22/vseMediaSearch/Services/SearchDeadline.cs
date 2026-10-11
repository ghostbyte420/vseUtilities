namespace vseMediaSearch.Services;

/// <summary>Bounds request waiting even if an external operation ignores cancellation.</summary>
internal static class SearchDeadline
{
    internal static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

    internal static async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan budget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (budget <= TimeSpan.Zero || budget.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(budget));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(budget);
        // Synchronous startup (including PATH probing) must not hold the request thread.
        var work = Task.Run(() => operation(deadline.Token), deadline.Token);
        try
        {
            return await work.WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException("Media search exceeded its time budget.", ex);
        }
        finally
        {
            // Cleanup can outlive the response. Keep its token usable and observe failures.
            _ = work.ContinueWith(completed =>
            {
                if (completed.IsFaulted)
                {
                    DiagnosticLog.Write("search.background.failed", $"error={completed.Exception!.GetBaseException().GetType().Name}");
                }
                deadline.Dispose();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}
