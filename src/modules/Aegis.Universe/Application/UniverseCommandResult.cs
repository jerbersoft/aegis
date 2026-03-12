namespace Aegis.Universe.Application;

public static class UniverseStatusCodes
{
    public const int Ok = 200;
    public const int Created = 201;
    public const int NoContent = 204;
}

public sealed class UniverseCommandResult<T>
{
    private UniverseCommandResult(bool succeeded, int statusCode, T? value, string? errorCode, string? errorMessage)
    {
        Succeeded = succeeded;
        StatusCode = statusCode;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }

    public int StatusCode { get; }

    public T? Value { get; }

    public string? ErrorCode { get; }

    public string? ErrorMessage { get; }

    public static UniverseCommandResult<T> Success(T value, int statusCode = UniverseStatusCodes.Ok) =>
        new(true, statusCode, value, null, null);

    public static UniverseCommandResult<T> Failure(string errorCode, string errorMessage, int statusCode) =>
        new(false, statusCode, default, errorCode, errorMessage);
}

public sealed class UniverseCommandResult
{
    private UniverseCommandResult(bool succeeded, int statusCode, string? errorCode, string? errorMessage)
    {
        Succeeded = succeeded;
        StatusCode = statusCode;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }

    public int StatusCode { get; }

    public string? ErrorCode { get; }

    public string? ErrorMessage { get; }

    public static UniverseCommandResult Success(int statusCode = UniverseStatusCodes.NoContent) =>
        new(true, statusCode, null, null);

    public static UniverseCommandResult Failure(string errorCode, string errorMessage, int statusCode) =>
        new(false, statusCode, errorCode, errorMessage);
}
