namespace Workleap.DotNet.CodingStandards.Tests.Helpers;

internal static class SharedHttpClient
{
    public static HttpClient Instance { get; } = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var socketHandler = new SocketsHttpHandler()
        {
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
        };

        return new HttpClient(new HttpRetryMessageHandler(socketHandler), disposeHandler: true);
    }
    private sealed class HttpRetryMessageHandler(HttpMessageHandler handler) : DelegatingHandler(handler)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            const int maxRetries = 5;
            var defaultDelay = TimeSpan.FromMilliseconds(200);
            for (var i = 1; ; i++, defaultDelay *= 2)
            {
                TimeSpan? delayHint = null;
                HttpResponseMessage? result = null;

                try
                {
                    result = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    if (!IsLastAttempt(i) && ((int)result.StatusCode >= 500 || result.StatusCode is System.Net.HttpStatusCode.RequestTimeout or System.Net.HttpStatusCode.TooManyRequests))
                    {
                        // Use "Retry-After" value, if available. Typically, this is sent with
                        // either a 503 (Service Unavailable) or 429 (Too Many Requests):
                        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Retry-After

                        delayHint = result.Headers.RetryAfter switch
                        {
                            { Date: { } date } => date - DateTimeOffset.UtcNow,
                            { Delta: { } delta } => delta,
                            _ => null,
                        };

                        result.Dispose();
                    }
                    else
                    {
                        return result;
                    }
                }
                catch (HttpRequestException)
                {
                    result?.Dispose();
                    if (IsLastAttempt(i))
                    {
                        throw;
                    }
                }
                catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken) // catch "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing"
                {
                    result?.Dispose();
                    if (IsLastAttempt(i))
                    {
                        throw;
                    }
                }

                await Task.Delay(delayHint is { } someDelay && someDelay > TimeSpan.Zero ? someDelay : defaultDelay, cancellationToken).ConfigureAwait(false);

                static bool IsLastAttempt(int i) => i >= maxRetries;
            }
        }
    }
}
