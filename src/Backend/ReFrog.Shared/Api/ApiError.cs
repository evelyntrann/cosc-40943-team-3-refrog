namespace ReFrog.Contracts.Api;

public sealed record ApiError(
    ApiErrorCategory Category,
    string Code,
    string Message,
    IReadOnlyDictionary<string, string>? Fields = null,
    DateTimeOffset? RetryAt = null);
