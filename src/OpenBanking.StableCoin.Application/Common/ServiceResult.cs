namespace OpenBanking.StableCoin.Application.Common;

public sealed class ServiceResult<T>
{
    public T? Data { get; private init; }
    public bool IsSuccess { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }
    public int HttpStatusHint { get; private init; }

    public static ServiceResult<T> Success(T data) =>
        new() { Data = data, IsSuccess = true, HttpStatusHint = 200 };

    public static ServiceResult<T> Failure(string errorCode, string message, int httpStatus = 400) =>
        new() { IsSuccess = false, ErrorCode = errorCode, ErrorMessage = message, HttpStatusHint = httpStatus };

    public static ServiceResult<T> NotFound(string message, string errorCode = "NOT_FOUND") =>
        Failure(errorCode, message, 404);

    public static ServiceResult<T> Unauthorized(string message = "Unauthorized.") =>
        Failure("UNAUTHORIZED", message, 401);
}
