using System.Diagnostics.CodeAnalysis;

namespace ArchitectureAssistant.Domain.Common;

public sealed class Result<T>
{
    private Result(T? value, IReadOnlyList<DomainError> errors)
    {
        Value = value;
        Errors = errors;
    }

    public bool IsSuccess => Errors.Count == 0;

    public T? Value { get; }

    public IReadOnlyList<DomainError> Errors { get; }

    public static Result<T> Success(T value) =>
        new(value, Array.Empty<DomainError>());

    public static Result<T> Failure(DomainError error) =>
        new(default, [error]);

    public static Result<T> Failure(IEnumerable<DomainError> errors) =>
        new(default, errors.ToArray());

    public bool TryGetValue([NotNullWhen(true)] out T? value)
    {
        value = Value;
        return IsSuccess && value is not null;
    }
}
