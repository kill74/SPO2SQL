namespace Bring.Utilities;

/// <summary>
/// Represents the outcome of an operation using modern C# pattern matching features.
/// This is a generic result type that can represent either success or failure states.
/// </summary>
/// <typeparam name="T">The type of value returned on success</typeparam>
public abstract record OperationResult<T>
{
    /// <summary>
    /// Creates a successful operation result with a value.
    /// </summary>
    /// <param name="value">The result value</param>
    /// <returns>A success result</returns>
    public static OperationResult<T> Success(T value) => new SuccessResult(value);

    /// <summary>
    /// Creates a failed operation result with an error message.
    /// </summary>
    /// <param name="error">The error message</param>
    /// <param name="errorCode">Optional error code for categorization</param>
    /// <returns>A failure result</returns>
    public static OperationResult<T> Failure(string error, string? errorCode = null) =>
        new FailureResult(error, errorCode);

    /// <summary>
    /// Creates a failed operation result from an exception.
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <returns>A failure result</returns>
    public static OperationResult<T> Failure(Exception exception) =>
        new FailureResult(exception.Message, exception.GetType().Name, exception);

    /// <summary>
    /// Represents a successful operation with a value.
    /// </summary>
    /// <param name="Value">The successful result value</param>
    public sealed record SuccessResult(T Value) : OperationResult<T>;

    /// <summary>
    /// Represents a failed operation with error details.
    /// </summary>
    /// <param name="Error">The error message</param>
    /// <param name="ErrorCode">Optional error code for categorization</param>
    /// <param name="Exception">Optional exception that caused the failure</param>
    public sealed record FailureResult(
        string Error,
        string? ErrorCode = null,
        Exception? Exception = null) : OperationResult<T>;
}

/// <summary>
/// Extension methods for OperationResult demonstrating pattern matching usage.
/// </summary>
public static class OperationResultExtensions
{
    /// <summary>
    /// Determines if the result is successful using pattern matching.
    /// Demonstrates: is pattern
    /// </summary>
    public static bool IsSuccess<T>(this OperationResult<T> result) =>
        result is OperationResult<T>.SuccessResult;

    /// <summary>
    /// Determines if the result is a failure using pattern matching.
    /// Demonstrates: is not pattern
    /// </summary>
    public static bool IsFailure<T>(this OperationResult<T> result) =>
        result is not OperationResult<T>.SuccessResult;

    /// <summary>
    /// Gets the value if successful, or throws if failed.
    /// Demonstrates: switch expression with pattern matching
    /// </summary>
    public static T GetValueOrThrow<T>(this OperationResult<T> result) =>
        result switch
        {
            OperationResult<T>.SuccessResult success => success.Value,
            OperationResult<T>.FailureResult failure => throw new InvalidOperationException(
                $"Operation failed: {failure.Error}",
                failure.Exception),
            _ => throw new InvalidOperationException("Unknown result type")
        };

    /// <summary>
    /// Gets the value if successful, or returns a default value if failed.
    /// Demonstrates: switch expression with discard pattern
    /// </summary>
    public static T? GetValueOrDefault<T>(this OperationResult<T> result, T? defaultValue = default) =>
        result switch
        {
            OperationResult<T>.SuccessResult success => success.Value,
            _ => defaultValue
        };

    /// <summary>
    /// Executes different actions based on success or failure.
    /// Demonstrates: switch statement with pattern matching
    /// </summary>
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

    /// <summary>
    /// Transforms a successful result to a new type.
    /// Demonstrates: switch expression with transformation
    /// </summary>
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

    /// <summary>
    /// Chains operations that return results.
    /// Demonstrates: switch expression with function chaining
    /// </summary>
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

    /// <summary>
    /// Gets error message or null if successful.
    /// Demonstrates: property pattern matching
    /// </summary>
    public static string? GetErrorMessage<T>(this OperationResult<T> result) =>
        result switch
        {
            OperationResult<T>.FailureResult { Error: var error } => error,
            _ => null
        };

    /// <summary>
    /// Categorizes the result based on error code patterns.
    /// Demonstrates: property patterns with when guards
    /// </summary>
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

    /// <summary>
    /// Validates a result based on complex property patterns.
    /// Demonstrates: nested property patterns and logical patterns
    /// </summary>
    public static bool IsRecoverableError<T>(this OperationResult<T> result) =>
        result switch
        {
            OperationResult<T>.FailureResult { ErrorCode: "Timeout" or "NetworkError" or "TemporaryUnavailable" } => true,
            OperationResult<T>.FailureResult { Exception: TimeoutException or HttpRequestException } => true,
            _ => false
        };
}

/// <summary>
/// Extension methods for working with collections of OperationResults.
/// Demonstrates: pattern matching with lists and collections
/// </summary>
public static class OperationResultCollectionExtensions
{
    /// <summary>
    /// Checks if all results in a collection are successful.
    /// Demonstrates: collection patterns with LINQ
    /// </summary>
    public static bool AllSuccessful<T>(this IEnumerable<OperationResult<T>> results) =>
        results.All(r => r is OperationResult<T>.SuccessResult);

    /// <summary>
    /// Checks if any result in a collection is a failure.
    /// Demonstrates: collection patterns with negation
    /// </summary>
    public static bool AnyFailures<T>(this IEnumerable<OperationResult<T>> results) =>
        results.Any(r => r is not OperationResult<T>.SuccessResult);

    /// <summary>
    /// Gets all successful values from a collection.
    /// Demonstrates: pattern matching in LINQ queries
    /// </summary>
    public static IEnumerable<T> GetSuccessfulValues<T>(this IEnumerable<OperationResult<T>> results) =>
        results
            .Where(r => r is OperationResult<T>.SuccessResult)
            .Select(r => ((OperationResult<T>.SuccessResult)r).Value);

    /// <summary>
    /// Gets all error messages from failed results.
    /// Demonstrates: pattern matching with collection transformation
    /// </summary>
    public static IEnumerable<string> GetErrors<T>(this IEnumerable<OperationResult<T>> results) =>
        results
            .OfType<OperationResult<T>.FailureResult>()
            .Select(f => f.Error);

    /// <summary>
    /// Combines multiple results into a single result.
    /// Demonstrates: switch expression with collection patterns
    /// </summary>
    public static OperationResult<IReadOnlyList<T>> Combine<T>(this IEnumerable<OperationResult<T>> results)
    {
        var resultList = results.ToList();
        
        return resultList switch
        {
            // Empty list
            [] => OperationResult<IReadOnlyList<T>>.Success(Array.Empty<T>()),
            
            // All successful - extract values
            var list when list.All(r => r is OperationResult<T>.SuccessResult) =>
                OperationResult<IReadOnlyList<T>>.Success(
                    list.Cast<OperationResult<T>.SuccessResult>()
                        .Select(s => s.Value)
                        .ToList()),
            
            // At least one failure - combine error messages
            var list => OperationResult<IReadOnlyList<T>>.Failure(
                string.Join("; ", list.GetErrors()),
                "MultipleErrors")
        };
    }

    /// <summary>
    /// Partitions results into successful and failed groups.
    /// Demonstrates: pattern matching for grouping
    /// </summary>
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

    /// <summary>
    /// Analyzes a collection of results and returns a summary.
    /// Demonstrates: complex pattern matching with statistics
    /// </summary>
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

/// <summary>
/// Summary of operation results for a collection.
/// Demonstrates: record types for immutable data structures
/// </summary>
/// <param name="TotalCount">Total number of results</param>
/// <param name="SuccessCount">Number of successful results</param>
/// <param name="FailureCount">Number of failed results</param>
/// <param name="ErrorsByCode">Dictionary of error codes and their counts</param>
public record ResultSummary<T>(
    int TotalCount,
    int SuccessCount,
    int FailureCount,
    IReadOnlyDictionary<string, int> ErrorsByCode)
{
    /// <summary>
    /// Gets the success rate as a percentage.
    /// Demonstrates: computed property in records
    /// </summary>
    public double SuccessRate => TotalCount > 0 ? (double)SuccessCount / TotalCount * 100 : 0;

    /// <summary>
    /// Categorizes the overall result quality.
    /// Demonstrates: switch expression on record properties
    /// </summary>
    public string QualityRating => this switch
    {
        { SuccessRate: 100 } => "Excellent",
        { SuccessRate: >= 90 } => "Good",
        { SuccessRate: >= 75 } => "Fair",
        { SuccessRate: >= 50 } => "Poor",
        _ => "Critical"
    };
}

/// <summary>
/// Validation result type using pattern matching for complex validation scenarios.
/// Demonstrates: discriminated unions with records
/// </summary>
public abstract record ValidationResult
{
    public static ValidationResult Valid() => new ValidResult();
    public static ValidationResult Invalid(params string[] errors) => new InvalidResult(errors);

    public sealed record ValidResult : ValidationResult;
    public sealed record InvalidResult(string[] Errors) : ValidationResult;

    /// <summary>
    /// Combines multiple validation results.
    /// Demonstrates: pattern matching with validation logic
    /// </summary>
    public static ValidationResult Combine(params ValidationResult[] results) =>
        results switch
        {
            [] => Valid(),
            var r when r.All(v => v is ValidResult) => Valid(),
            var r => Invalid(r.OfType<InvalidResult>()
                              .SelectMany(i => i.Errors)
                              .ToArray())
        };
}

/// <summary>
/// Extension methods for validation using pattern matching.
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Validates a string is not null or empty.
    /// Demonstrates: pattern matching for validation
    /// </summary>
    public static ValidationResult ValidateNotEmpty(this string? value, string fieldName) =>
        value switch
        {
            null => ValidationResult.Invalid($"{fieldName} cannot be null"),
            "" => ValidationResult.Invalid($"{fieldName} cannot be empty"),
            { Length: > 1000 } => ValidationResult.Invalid($"{fieldName} exceeds maximum length"),
            _ => ValidationResult.Valid()
        };

    /// <summary>
    /// Validates a number is within a range.
    /// Demonstrates: relational patterns
    /// </summary>
    public static ValidationResult ValidateRange(this int value, int min, int max, string fieldName) =>
        value switch
        {
            < 0 when min >= 0 => ValidationResult.Invalid($"{fieldName} cannot be negative"),
            var v when v < min => ValidationResult.Invalid($"{fieldName} must be at least {min}"),
            var v when v > max => ValidationResult.Invalid($"{fieldName} must be at most {max}"),
            _ => ValidationResult.Valid()
        };

    /// <summary>
    /// Validates a collection is not empty and within size limits.
    /// Demonstrates: list patterns
    /// </summary>
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
