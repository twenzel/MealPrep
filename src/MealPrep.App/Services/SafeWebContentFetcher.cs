using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MealPrep.App.Services;

public sealed class SafeWebContentFetcher(
    IHttpClientFactory httpClientFactory,
    RecipeWebImportOptions options,
    ILogger<SafeWebContentFetcher> logger)
{
    public const string HttpClientName = "web-recipe-import";

    public async Task<FetchedWebPage> FetchHtmlAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        if (!WebImportUrl.TryNormalizeHttps(sourceUrl, out var currentUri))
        {
            throw new WebRecipeImportException(
                "Bitte eine öffentliche HTTPS-Adresse zu einer Rezeptseite eingeben.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.HttpTimeoutSeconds));
        var client = httpClientFactory.CreateClient(HttpClientName);

        for (var redirect = 0; redirect <= options.MaximumRedirects; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.8");
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            if (IsRedirect(response.StatusCode))
            {
                if (redirect == options.MaximumRedirects || response.Headers.Location is null)
                {
                    throw new WebRecipeImportException(
                        "Die Rezeptseite leitet zu oft weiter.");
                }

                var nextUri = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(currentUri, response.Headers.Location);
                if (!WebImportUrl.TryNormalizeHttps(nextUri.ToString(), out currentUri))
                {
                    throw new WebRecipeImportException(
                        "Die Rezeptseite leitet auf eine nicht erlaubte Adresse weiter.");
                }

                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Recipe page request for host {Host} returned {StatusCode}.",
                    currentUri.Host,
                    (int)response.StatusCode);
                throw new WebRecipeImportException(
                    "Die Rezeptseite konnte nicht geladen werden.");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(mediaType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
            {
                throw new WebRecipeImportException(
                    "Die Adresse liefert keine unterstützte HTML-Seite.");
            }

            var bytes = await ReadLimitedBytesAsync(
                response.Content,
                options.MaximumHtmlBytes,
                timeout.Token);
            return new FetchedWebPage(
                currentUri,
                DecodeHtml(bytes, response.Content.Headers.ContentType?.CharSet));
        }

        throw new WebRecipeImportException("Die Rezeptseite konnte nicht geladen werden.");
    }

    public async Task<ImportedRecipeImage?> TryFetchImageAsync(
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        if (!WebImportUrl.TryNormalizeHttps(imageUrl, out var currentUri))
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.HttpTimeoutSeconds));
        var client = httpClientFactory.CreateClient(HttpClientName);

        try
        {
            for (var redirect = 0; redirect <= Math.Min(options.MaximumRedirects, 2); redirect++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                request.Headers.Accept.ParseAdd("image/jpeg,image/png,image/webp");
                using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token);

                if (IsRedirect(response.StatusCode))
                {
                    if (response.Headers.Location is null)
                    {
                        return null;
                    }

                    var nextUri = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location
                        : new Uri(currentUri, response.Headers.Location);
                    if (!WebImportUrl.TryNormalizeHttps(nextUri.ToString(), out currentUri))
                    {
                        return null;
                    }

                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var bytes = await ReadLimitedBytesAsync(
                    response.Content,
                    options.MaximumImageBytes,
                    timeout.Token);
                var mediaType = ImageContentTypeDetector.Detect(bytes);
                return mediaType is null
                    ? null
                    : new ImportedRecipeImage(bytes, mediaType, currentUri.ToString());
            }
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or OperationCanceledException or WebRecipeImportException)
        {
            logger.LogInformation(
                exception,
                "Recipe image candidate from host {Host} could not be loaded.",
                currentUri.Host);
        }

        return null;
    }

    private static async Task<byte[]> ReadLimitedBytesAsync(
        HttpContent content,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > 0 &&
            content.Headers.ContentLength > maximumBytes)
        {
            throw new WebRecipeImportException(
                "Die geladene Datei ist größer als erlaubt.");
        }

        await using var input = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream(Math.Min(maximumBytes, 64 * 1024));
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                if (output.Length + read > maximumBytes)
                {
                    throw new WebRecipeImportException(
                        "Die geladene Datei ist größer als erlaubt.");
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            return output.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string DecodeHtml(byte[] bytes, string? charset)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(charset)
                ? Encoding.GetEncoding(charset.Trim('"')).GetString(bytes)
                : Encoding.UTF8.GetString(bytes);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8.GetString(bytes);
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Redirect
            or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;
}

public sealed record FetchedWebPage(Uri FinalUri, string Html);

public static class WebImportUrl
{
    public static bool TryNormalizeHttps(string? value, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var candidate) ||
            candidate.Scheme != Uri.UriSchemeHttps ||
            candidate.Port != 443 ||
            !string.IsNullOrEmpty(candidate.UserInfo) ||
            string.IsNullOrWhiteSpace(candidate.IdnHost))
        {
            return false;
        }

        var host = candidate.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (host is "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal) ||
            host.EndsWith(".local", StringComparison.Ordinal))
        {
            return false;
        }

        if (IPAddress.TryParse(host, out var address) && !PublicNetworkAddress.IsPublic(address))
        {
            return false;
        }

        var builder = new UriBuilder(candidate)
        {
            Host = host,
            Fragment = string.Empty
        };
        uri = builder.Uri;
        return true;
    }
}

public static class PublicNetworkAddress
{
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.None) ||
            address.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] switch
            {
                0 or 10 or 127 => false,
                100 when bytes[1] is >= 64 and <= 127 => false,
                169 when bytes[1] == 254 => false,
                172 when bytes[1] is >= 16 and <= 31 => false,
                192 when bytes[1] == 0 && bytes[2] == 0 => false,
                192 when bytes[1] == 0 && bytes[2] == 2 => false,
                192 when bytes[1] == 168 => false,
                198 when bytes[1] is 18 or 19 => false,
                198 when bytes[1] == 51 && bytes[2] == 100 => false,
                203 when bytes[1] == 0 && bytes[2] == 113 => false,
                >= 224 => false,
                _ => true
            };
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6 ||
            address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
        {
            return false;
        }

        return (bytes[0] & 0xFE) != 0xFC &&
               !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8);
    }
}

public static class PublicInternetHttpHandler
{
    public static SocketsHttpHandler Create() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        AutomaticDecompression = DecompressionMethods.Brotli |
                                 DecompressionMethods.GZip |
                                 DecompressionMethods.Deflate,
        ConnectCallback = ConnectToValidatedAddressAsync
    };

    private static async ValueTask<Stream> ConnectToValidatedAddressAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(
            context.DnsEndPoint.Host,
            cancellationToken);
        if (addresses.Length == 0 || addresses.Any(address => !PublicNetworkAddress.IsPublic(address)))
        {
            throw new HttpRequestException("The target host does not resolve exclusively to public addresses.");
        }

        Exception? lastException = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            try
            {
                await socket.ConnectAsync(
                    new IPEndPoint(address, context.DnsEndPoint.Port),
                    cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                lastException = exception;
                if (exception is OperationCanceledException)
                {
                    throw;
                }
            }
        }

        throw new HttpRequestException("No public target address could be reached.", lastException);
    }
}

public static class ImageContentTypeDetector
{
    public static string? Detect(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 8 &&
            data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (data.Length >= 12 &&
            data[..4].SequenceEqual("RIFF"u8) &&
            data.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }
}
