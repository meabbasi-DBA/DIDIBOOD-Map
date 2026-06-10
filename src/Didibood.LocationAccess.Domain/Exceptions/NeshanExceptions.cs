namespace Didibood.LocationAccess.Domain.Exceptions;

public abstract class NeshanException : Exception
{
    protected NeshanException(string message, int? code = null, int? httpStatus = null)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
    }

    public int? Code { get; }
    public int? HttpStatus { get; }
}

public sealed class NeshanInvalidArgumentException : NeshanException
{
    public NeshanInvalidArgumentException(string message) : base(message, 400, 400) { }
}

public sealed class NeshanCoordinateException : NeshanException
{
    public NeshanCoordinateException(string message) : base(message, 470, 470) { }
}

public sealed class NeshanAuthenticationException : NeshanException
{
    public NeshanAuthenticationException(string message, int code) : base(message, code, code) { }
}

public sealed class NeshanAuthorizationException : NeshanException
{
    public NeshanAuthorizationException(string message, int code) : base(message, code, code) { }
}

public sealed class NeshanQuotaExceededException : NeshanException
{
    public NeshanQuotaExceededException(string message) : base(message, 481, 481) { }
}

public sealed class NeshanRateLimitException : NeshanException
{
    public NeshanRateLimitException(string message) : base(message, 482, 482) { }
}

public sealed class NeshanServiceException : NeshanException
{
    public NeshanServiceException(string message, int? code = 500, int? httpStatus = 500)
        : base(message, code, httpStatus) { }
}

public sealed class NeshanRenderTimeoutException : NeshanException
{
    public NeshanRenderTimeoutException(string message) : base(message, 503, 503) { }
}

public sealed class NeshanOverloadedException : NeshanException
{
    public NeshanOverloadedException(string message) : base(message, 503, 503) { }
}
