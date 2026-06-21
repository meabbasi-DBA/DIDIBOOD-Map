using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Configuration;
using Didibood.LocationAccess.Application.Neshan;
using Didibood.LocationAccess.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Didibood.LocationAccess.Infrastructure.Neshan;

public sealed class NeshanSearchClient(
    HttpClient httpClient,
    IOptions<NeshanOptions> options,
    ISystemConfigurationStore configStore,
    ILogger<NeshanSearchClient> logger) : INeshanSearchClient
{
    private const string SearchBaseUrl = "https://api.neshan.org/v1/search";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<NeshanSearchResponse> SearchAsync(
        string term,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        ValidateSearchInput(term, latitude, longitude);

        var apiKey = await configStore.GetAsync("neshan.SearchApiKey", options.Value.GetSearchApiKey(), cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new NeshanAuthenticationException("Neshan API key is not configured.", 480);

        var url = BuildUrl(term, latitude, longitude);
        logger.LogInformation("Neshan Search request URL: {Url}", url);

        Exception? lastNetworkError = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (attempt > 0)
                logger.LogWarning("Retrying Neshan Search after network failure (attempt {Attempt})", attempt + 1);

            try
            {
                return await SendSearchRequestAsync(url, apiKey, cancellationToken);
            }
            catch (NeshanException)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastNetworkError = ex;
            }
        }

        throw new NeshanServiceException(
            $"Neshan Search network failure: {lastNetworkError?.Message}",
            httpStatus: 503);
    }

    private async Task<NeshanSearchResponse> SendSearchRequestAsync(
        string url,
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Api-Key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);

        var stopwatch = Stopwatch.StartNew();
        using var response = await httpClient.SendAsync(request, timeoutCts.Token);
        var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        stopwatch.Stop();

        logger.LogInformation(
            "Neshan Search completed in {ElapsedMs}ms with HTTP {StatusCode}",
            stopwatch.Elapsed.TotalMilliseconds,
            (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            var error = NeshanExceptionMapper.TryParseError(body);
            var code = error?.Code > 0 ? error.Code : (int)response.StatusCode;
            throw NeshanExceptionMapper.Map(code, error);
        }

        var result = JsonSerializer.Deserialize<NeshanSearchResponse>(body, JsonOptions)
                     ?? new NeshanSearchResponse();

        result.Items ??= [];
        if (result.Count <= 0 && result.Items.Count > 0)
            result.Count = result.Items.Count;

        return result;
    }

    private static void ValidateSearchInput(string term, double latitude, double longitude)
    {
        if (string.IsNullOrWhiteSpace(term))
            throw new NeshanInvalidArgumentException("Search term is required.");

        if (latitude is < -90 or > 90)
            throw new NeshanCoordinateException("Latitude must be between -90 and 90.");

        if (longitude is < -180 or > 180)
            throw new NeshanCoordinateException("Longitude must be between -180 and 180.");
    }

    private static string BuildUrl(string term, double latitude, double longitude)
    {
        var query = new Dictionary<string, string>
        {
            ["term"] = term,
            ["lat"] = latitude.ToString(CultureInfo.InvariantCulture),
            ["lng"] = longitude.ToString(CultureInfo.InvariantCulture)
        };

        var qs = string.Join('&', query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return $"{SearchBaseUrl}?{qs}";
    }
}
