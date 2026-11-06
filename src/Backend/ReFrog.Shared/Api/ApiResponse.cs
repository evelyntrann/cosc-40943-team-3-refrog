namespace ReFrog.Contracts.Api;

public sealed record ApiResponse<T>
{
    internal ApiResponse(T? data, ApiError? error)
    {
        Data = data;
        Error = error;
    }

    public T? Data { get; }

    public ApiError? Error { get; }

}
public static class ApiResponse
{
    public static ApiResponse<T> Success<T>(T data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new ApiResponse<T>(data, null);
    }

    public static ApiResponse<T> Failure<T>(ApiError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ApiResponse<T>(default, error);
    }
}
