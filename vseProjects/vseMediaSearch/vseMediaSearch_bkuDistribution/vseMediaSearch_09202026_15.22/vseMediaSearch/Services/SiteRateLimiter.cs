namespace vseMediaSearch.Services;

/// <summary>
/// A lightweight, in-process, per-domain politeness delay so this tool doesn't hammer an
/// allow-listed site with rapid-fire requests (e.g. multi-page article pagination). Before
/// each HTTP call to a domain, callers should await <see cref="WaitTurnAsync"/>, which
/// blocks only long enough to space out consecutive requests to THAT domain by at least
/// <see cref="MinDelayBetweenRequests"/>. Requests to different domains are never blocked
/// by each other.
/// </summary>
internal static class SiteRateLimiter
{
    private static readonly TimeSpan MinDelayBetweenRequests = TimeSpan.FromSeconds(1);

    // Bounded resource use (Rule 21C): one entry per distinct host actually contacted, not
    // an unbounded structure driven directly by arbitrary external input volume.
    private static readonly Dictionary<string, DateTimeOffset> LastRequestByHost = [];
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>
    /// Waits, if necessary, until at least <see cref="MinDelayBetweenRequests"/> has elapsed
    /// since the last request to the same host as <paramref name="url"/>, then records this
    /// request's timestamp for future calls.
    /// </summary>
    public static async Task WaitTurnAsync(string url, CancellationToken cancellationToken = default)
    {
        var host = GetHost(url);
        if (host is null)
        {
            return;
        }

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        TimeSpan delay;
        try
        {
            if (LastRequestByHost.TryGetValue(host, out var lastRequestAt))
            {
                var elapsed = DateTimeOffset.UtcNow - lastRequestAt;
                delay = elapsed < MinDelayBetweenRequests ? MinDelayBetweenRequests - elapsed : TimeSpan.Zero;
            }
            else
            {
                delay = TimeSpan.Zero;
            }

            LastRequestByHost[host] = DateTimeOffset.UtcNow + delay;
        }
        finally
        {
            Gate.Release();
        }

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string? GetHost(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host.ToLowerInvariant() : null;
    }
}
