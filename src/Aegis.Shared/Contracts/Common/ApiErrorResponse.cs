namespace Aegis.Shared.Contracts.Common;

public sealed record ApiErrorResponse(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null);
