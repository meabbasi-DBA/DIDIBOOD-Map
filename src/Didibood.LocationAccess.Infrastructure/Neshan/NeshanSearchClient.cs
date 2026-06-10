using System.Net.Http.Headers;
using System.Net.Http.Json;
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
    ILogger<NeshanSearchClient> logger) : INeshanSearchClient
{
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
        var apiKey = options.Value.GetSearchApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new NeshanAuthenticationException("Neshan API key is not configured.", 480);
        }

        var url =
            $"v1/search?term={Uri.EscapeDataString(term)}&lat={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lng={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Api-Key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        logger.LogDebug("Neshan Search: term={Term}, lat={Lat}, lng={Lng}", term, latitude, longitude);

        var started = DateTime.UtcNow;
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var elapsed = DateTime.UtcNow - started;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        logger.LogInformation(
            "Neshan Search completed in {ElapsedMs}ms with HTTP {StatusCode}",
            elapsed.TotalMilliseconds,
            (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            var error = NeshanExceptionMapper.TryParseError(body);
            throw NeshanExceptionMapper.Map((int)response.StatusCode, error);
        }

        var result = JsonSerializer.Deserialize<NeshanSearchResponse>(body, JsonOptions)
                     ?? new NeshanSearchResponse();

        if (result.Items is null)
        {
            result.Items = [];
        }

        return result;
    }
}
