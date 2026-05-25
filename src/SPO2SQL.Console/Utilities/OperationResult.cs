namespace SPO2SQL.Utilities;

public abstract record OperationResult<T>
{
    public static OperationResult<T> Success(T value) => new SuccessResult(value);

    public static OperationResult<T> Failure(string error, string? errorCode = null) =>
        new FailureResult(error, errorCode);

    public static OperationResult<T> Failure(Exception exception) =>
        new FailureResult(exception.Message, exception.GetType().Name, exception);

    public sealed record SuccessResult(T Value) : OperationResult<T>;

    public sealed record FailureResult(
        string Error,
        string? ErrorCode = null,
        Exception? Exception = null) : OperationResult<T>;
}

public static class OperationResultExtensions
{
    public static bool IsSuccess<T>(this OperationResult<T> result) =>
        result is OperationResult<T>.SuccessResult;

    public static bool IsFailure<T>(this OperationResult<T> result) =>
        result is not OperationResult<T>.SuccessResult;

    public static T GetValueOrThrow<T>(this OperationResult<T> result) =>
        result switch
        {
            OperationResult<T>.SuccessResult success => success.Value,
            OperationResult<T>.FailureResult failure => throw new InvalidOperationException(
                $"Operation failed: {failure.Error}",
                failure.Exception),
            _ => throw new InvalidOperationException("Unknown result type")
        };

    public static T? GetValueOrDefault<T>(this OperationResult<T> result, T? defaultValue = default) =>
        result switch
        {
            OperationResult<T>.SuccessResult success => success.Value,
            _ => defaultValue
        };

    public static void Match<T>(
        this OperationResult<T> result,
        Action<T> onSuccess,
        Action<string, string?> onFailure)
    {
        switch (result)
        {
            case OperationResult<T>.SuccessResult success:
                onSuccess(success.Value);
                break;
            case OperationResult<T>.FailureResult failure:
                onFailure(failure.Error, failure.ErrorCode);
                break;
        }
    }

    public static OperationResult<TResult> Map<T, TResult>(
        this OperationResult<T> result,
        Func<T, TResult> mapper) =>
        result switch
        {
            OperationResult<T>.SuccessResult success =>
                OperationResult<TResult>.Success(mapper(success.Value)),
            OperationResult<T>.FailureResult failure =>
                OperationResult<TResult>.Failure(failure.Error, failure.ErrorCode),
            _ => throw new InvalidOperationException("Unknown result type")
        };

    public static OperationResult<TResult> Bind<T, TResult>(
        this OperationResult<T> result,
        Func<T, OperationResult<TResult>> binder) =>
        result switch
        {
            OperationResult<T>.SuccessResult success => binder(success.Value),
            OperationResult<T>.FailureResult failure =>
                OperationResult<TResult>.Failure(failure.Error, failure.ErrorCode),
            _ => throw new InvalidOperationException("Unknown result type")
        };

    public static string? GetErrorMessage<T>(this OperationResult<T> result) =>
        result switch
        {
            OperationResult<T>.FailureResult { Error: var error } => error,
            _ => null
        };

    public static string CategorizeError<T>(this OperationResult<T> result) =>
        result switch
        {
            OperationResult<T>.SuccessResult => "Success",
            OperationResult<T>.FailureResult { ErrorCode: "NotFound" } => "Resource Not Found",
            OperationResult<T>.FailureResult { ErrorCode: var code } when code?.StartsWith("Auth") == true =>
                "Authentication/Authorization Error",
            OperationResult<T>.FailureResult { ErrorCode: var code } when code?.StartsWith("Validation") == true =>
                "Validation Error",
            OperationResult<T>.FailureResult { Exception: not null } => "Exception Occurred",
            OperationResult<T>.FailureResult => "General Error",
            _ => "Unknown"
        };

    public static bool IsRecoverableError<T>(this OperationResult<T> result) =>
        result switch
        {
            OperationResult<T>.FailureResult { ErrorCode: "Timeout" or "NetworkError" or "TemporaryUnavailable" } => true,
            OperationResult<T>.FailureResult { Exception: TimeoutException or HttpRequestException } => true,
            _ => false
        };
}

public static class OperationResultCollectionExtensions
{
    public static bool AllSuccessful<T>(this IEnumerable<OperationResult<T>> results) =>
        results.All(r => r is OperationResult<T>.SuccessResult);

    public static bool AnyFailures<T>(this IEnumerable<OperationResult<T>> results) =>
        results.Any(r => r is not OperationResult<T>.SuccessResult);

    public static IEnumerable<T> GetSuccessfulValues<T>(this IEnumerable<OperationResult<T>> results) =>
        results
            .Where(r => r is OperationResult<T>.SuccessResult)
            .Select(r => ((OperationResult<T>.SuccessResult)r).Value);

    public static IEnumerable<string> GetErrors<T>(this IEnumerable<OperationResult<T>> results) =>
        results
            .OfType<OperationResult<T>.FailureResult>()
            .Select(f => f.Error);

    public static OperationResult<IReadOnlyList<T>> Combine<T>(this IEnumerable<OperationResult<T>> results)
    {
        var resultList = results.ToList();

        return resultList switch
        {
        [] => OperationResult<IReadOnlyList<T>>.Success(Array.Empty<T>()),

            var list when list.All(r => r is OperationResult<T>.SuccessResult) =>
                OperationResult<IReadOnlyList<T>>.Success(
                    list.Cast<OperationResult<T>.SuccessResult>()
                        .Select(s => s.Value)
                        .ToList()),

            var list => OperationResult<IReadOnlyList<T>>.Failure(
                string.Join("; ", list.GetErrors()),
                "MultipleErrors")
        };
    }

    public static (IReadOnlyList<T> Successful, IReadOnlyList<string> Failed) Partition<T>(
        this IEnumerable<OperationResult<T>> results)
    {
        var successful = new List<T>();
        var failed = new List<string>();

        foreach (var result in results)
        {
            switch (result)
            {
                case OperationResult<T>.SuccessResult success:
                    successful.Add(success.Value);
                    break;
                case OperationResult<T>.FailureResult failure:
                    failed.Add(failure.Error);
                    break;
            }
        }

        return (successful, failed);
    }

    public static ResultSummary<T> Summarize<T>(this IEnumerable<OperationResult<T>> results)
    {
        var resultList = results.ToList();
        var successCount = resultList.Count(r => r is OperationResult<T>.SuccessResult);
        var failureCount = resultList.Count - successCount;

        var errorsByCode = resultList
            .OfType<OperationResult<T>.FailureResult>()
            .Where(f => f.ErrorCode is not null)
            .GroupBy(f => f.ErrorCode!)
            .ToDictionary(g => g.Key, g => g.Count());

        return new ResultSummary<T>(
            TotalCount: resultList.Count,
            SuccessCount: successCount,
            FailureCount: failureCount,
            ErrorsByCode: errorsByCode
        );
    }
}

public record ResultSummary<T>(
    int TotalCount,
    int SuccessCount,
    int FailureCount,
    IReadOnlyDictionary<string, int> ErrorsByCode)
{
    public double SuccessRate => TotalCount > 0 ? (double)SuccessCount / TotalCount * 100 : 0;

    public string QualityRating => this switch
    {
        { SuccessRate: 100 } => "Excellent",
        { SuccessRate: >= 90 } => "Good",
        { SuccessRate: >= 75 } => "Fair",
        { SuccessRate: >= 50 } => "Poor",
        _ => "Critical"
    };
}

public abstract record ValidationResult
{
    public static ValidationResult Valid() => new ValidResult();
    public static ValidationResult Invalid(params string[] errors) => new InvalidResult(errors);

    public sealed record ValidResult : ValidationResult;
    public sealed record InvalidResult(string[] Errors) : ValidationResult;

    public static ValidationResult Combine(params ValidationResult[] results)
    {
        results ??= [];
        return results switch
        {
        [] => Valid(),
            var r when r.All(v => v is ValidResult) => Valid(),
            var r => Invalid(r.OfType<InvalidResult>()
                              .SelectMany(i => i.Errors)
                              .ToArray())
        };
    }
}

public static class ValidationExtensions
{
    public static ValidationResult ValidateNotEmpty(this string? value, string fieldName) =>
        value switch
        {
            null => ValidationResult.Invalid($"{fieldName} cannot be null"),
            "" => ValidationResult.Invalid($"{fieldName} cannot be empty"),
            { Length: > 1000 } => ValidationResult.Invalid($"{fieldName} exceeds maximum length"),
            _ => ValidationResult.Valid()
        };

    public static ValidationResult ValidateRange(this int value, int min, int max, string fieldName) =>
        value switch
        {
            < 0 when min >= 0 => ValidationResult.Invalid($"{fieldName} cannot be negative"),
            var v when v < min => ValidationResult.Invalid($"{fieldName} must be at least {min}"),
            var v when v > max => ValidationResult.Invalid($"{fieldName} must be at most {max}"),
            _ => ValidationResult.Valid()
        };

    public static ValidationResult ValidateCollection<T>(
        this IEnumerable<T>? collection,
        string fieldName,
        int? minCount = null,
        int? maxCount = null) =>
        collection?.ToList() switch
        {
            null => ValidationResult.Invalid($"{fieldName} cannot be null"),
            [] when minCount > 0 => ValidationResult.Invalid($"{fieldName} must contain at least {minCount} items"),
            var list when minCount.HasValue && list.Count < minCount =>
                ValidationResult.Invalid($"{fieldName} must contain at least {minCount} items"),
            var list when maxCount.HasValue && list.Count > maxCount =>
                ValidationResult.Invalid($"{fieldName} must contain at most {maxCount} items"),
            _ => ValidationResult.Valid()
        };
}
