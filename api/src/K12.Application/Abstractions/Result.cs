namespace K12.Application.Abstractions;

public enum ErrorKind { NotFound, Validation, Conflict, Forbidden }

public sealed record Error(ErrorKind Kind, string Code, string Message)
{
    public static Error NotFound(string code, string message) => new(ErrorKind.NotFound, code, message);
    public static Error Validation(string code, string message) => new(ErrorKind.Validation, code, message);
    public static Error Conflict(string code, string message) => new(ErrorKind.Conflict, code, message);
}

/// <summary>The outcome of a use case: a value, or an expected error. Exceptions are kept for bugs.</summary>
public sealed class Result<T>
{
    private Result(T? value, Error? error) { Value = value; Error = error; }

    public T? Value { get; }
    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(Error error) => new(default, error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
