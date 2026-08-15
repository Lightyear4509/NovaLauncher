using System.Net;

namespace NovaLauncher.Infrastructure.Enrichment;

public sealed record BoundedHttpResult(
    HttpStatusCode StatusCode,
    byte[]? Content,
    bool IsOffline,
    string? Error,
    string? ContentType = null);

public interface IBoundedHttpClient
{
    Task<BoundedHttpResult> GetAsync(
        Uri uri,
        IReadOnlyDictionary<string, string>? headers,
        int maximumBytes,
        CancellationToken cancellationToken);
}

public sealed class BoundedHttpClient(HttpClient client, TimeProvider timeProvider) : IBoundedHttpClient
{
    private const int MaximumAttempts = 3;
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(2);

    public async Task<BoundedHttpResult> GetAsync(
        Uri uri,
        IReadOnlyDictionary<string, string>? headers,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return new BoundedHttpResult(HttpStatusCode.BadRequest, null, false, "Only HTTPS provider requests are allowed.");
        }

        if (maximumBytes is <= 0 or > 10 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                if (headers is not null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (IsTransient(response.StatusCode) && attempt < MaximumAttempts)
                {
                    var delay = response.Headers.RetryAfter?.Delta is { } retryAfter
                        ? TimeSpan.FromTicks(Math.Min(retryAfter.Ticks, MaximumRetryDelay.Ticks))
                        : TimeSpan.FromMilliseconds(100 * attempt);
                    await Task.Delay(delay, timeProvider, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return new BoundedHttpResult(response.StatusCode, null, false, $"Provider returned HTTP {(int)response.StatusCode}.");
                }

                if (response.Content.Headers.ContentLength is > 0 && response.Content.Headers.ContentLength > maximumBytes)
                {
                    return new BoundedHttpResult(response.StatusCode, null, false, "Provider response exceeds the size limit.");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var buffer = new MemoryStream(Math.Min(maximumBytes, 64 * 1024));
                var chunk = new byte[16 * 1024];
                while (true)
                {
                    var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        return new BoundedHttpResult(
                            response.StatusCode,
                            buffer.ToArray(),
                            false,
                            null,
                            response.Content.Headers.ContentType?.MediaType);
                    }

                    if (buffer.Length + read > maximumBytes)
                    {
                        return new BoundedHttpResult(response.StatusCode, null, false, "Provider response exceeds the size limit.");
                    }

                    await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (HttpRequestException exception) when (attempt == MaximumAttempts)
            {
                return new BoundedHttpResult(0, null, true, exception.Message);
            }
            catch (HttpRequestException) when (attempt < MaximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), timeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt == MaximumAttempts)
            {
                return new BoundedHttpResult(0, null, true, "Provider request timed out.");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < MaximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        return new BoundedHttpResult(0, null, true, "Provider request failed.");
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
}
