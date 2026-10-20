namespace ColdRoomAssessment.Application;

public enum OperationStatus
{
    Success,
    Invalid,
    NotFound,
    Conflict
}

public sealed record OperationResult<T>(OperationStatus Status, T? Value, string? Error)
{
    public static OperationResult<T> Success(T value) => new(OperationStatus.Success, value, null);

    public static OperationResult<T> Invalid(string error) => new(OperationStatus.Invalid, default, error);

    public static OperationResult<T> NotFound(string error) => new(OperationStatus.NotFound, default, error);

    public static OperationResult<T> Conflict(string error) => new(OperationStatus.Conflict, default, error);
}
