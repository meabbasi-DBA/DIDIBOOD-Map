using System.Text.Json;
using Didibood.LocationAccess.Application.Neshan;
using Didibood.LocationAccess.Domain.Exceptions;

namespace Didibood.LocationAccess.Infrastructure.Neshan;

internal static class NeshanExceptionMapper
{
    public static NeshanException Map(int httpStatus, NeshanErrorResponse? error)
    {
        var message = error?.Message ?? $"Neshan API error (HTTP {httpStatus})";
        var code = error?.Code ?? httpStatus;

        return code switch
        {
            400 => new NeshanInvalidArgumentException(message),
            470 => new NeshanCoordinateException(message),
            480 or 483 => new NeshanAuthenticationException(message, code),
            481 => new NeshanQuotaExceededException(message),
            482 => new NeshanRateLimitException(message),
            484 or 485 => new NeshanAuthorizationException(message, code),
            503 when message.Contains("render_timeout", StringComparison.OrdinalIgnoreCase)
                => new NeshanRenderTimeoutException(message),
            503 when message.Contains("overloaded", StringComparison.OrdinalIgnoreCase)
                => new NeshanOverloadedException(message),
            500 or 503 => new NeshanServiceException(message, code, httpStatus),
            _ => new NeshanServiceException(message, code, httpStatus)
        };
    }

    public static NeshanErrorResponse? TryParseError(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<NeshanErrorResponse>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
