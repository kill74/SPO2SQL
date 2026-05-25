using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SharePoint.Client;
using SPO2SQL.Logging;

namespace SPO2SQL.SharePoint;

public class RetryPolicy
{
    private readonly int _maxRetries;
    private readonly int _initialDelayMs;

    public RetryPolicy(int maxRetries = 3, int initialDelayMs = 1000)
    {
        if (maxRetries < 1)
        {
            throw new ArgumentException("Maximum retries must be at least 1.", nameof(maxRetries));
        }

        if (initialDelayMs < 100)
        {
            throw new ArgumentException("Initial delay must be at least 100ms.", nameof(initialDelayMs));
        }

        _maxRetries = maxRetries;
        _initialDelayMs = initialDelayMs;
    }

    public T ExecuteWithRetry<T>(Func<T> operation, string operationName = "Unknown")
    {
        ArgumentNullException.ThrowIfNull(operation);

        int attempt = 0;

        while (attempt <= _maxRetries)
        {
            try
            {
                if (attempt > 0)
                {
                    Logger.LogWarning($"Retry attempt {attempt} of {_maxRetries} for operation '{operationName}'");
                }

                return operation();
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt <= _maxRetries)
            {
                attempt++;
                if (attempt > _maxRetries)
                {
                    throw new InvalidOperationException($"Operation '{operationName}' failed after {_maxRetries + 1} attempts.", ex);
                }

                int delayMs = CalculateBackoffDelay(attempt);

                Logger.LogWarning(
                    $"Transient error in '{operationName}': {ex.Message}. " +
                    $"Retrying in {delayMs}ms (attempt {attempt}/{_maxRetries})");

                Thread.Sleep(delayMs);
            }
        }

        throw new InvalidOperationException($"Operation '{operationName}' failed after {_maxRetries + 1} attempts.");
    }

    public T ExecuteWithRetry<T>(Func<T> operation, CancellationToken cancellationToken, string operationName = "Unknown")
    {
        ArgumentNullException.ThrowIfNull(operation);

        int attempt = 0;

        while (attempt <= _maxRetries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (attempt > 0)
                {
                    Logger.LogWarning($"Retry attempt {attempt} of {_maxRetries} for operation '{operationName}'");
                }

                return operation();
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt <= _maxRetries)
            {
                attempt++;
                if (attempt > _maxRetries)
                {
                    throw new InvalidOperationException($"Operation '{operationName}' failed after {_maxRetries + 1} attempts.", ex);
                }

                int delayMs = CalculateBackoffDelay(attempt);

                Logger.LogWarning(
                    $"Transient error in '{operationName}': {ex.Message}. " +
                    $"Retrying in {delayMs}ms (attempt {attempt}/{_maxRetries})");

                cancellationToken.WaitHandle.WaitOne(delayMs);
            }
        }

        throw new InvalidOperationException($"Operation '{operationName}' failed after {_maxRetries + 1} attempts.");
    }

    public void ExecuteWithRetry(Action operation, CancellationToken cancellationToken, string operationName = "Unknown")
    {
        ArgumentNullException.ThrowIfNull(operation);

        int attempt = 0;

        while (attempt <= _maxRetries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (attempt > 0)
                {
                    Logger.LogWarning($"Retry attempt {attempt} of {_maxRetries} for operation '{operationName}'");
                }

                operation();
                return;
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt <= _maxRetries)
            {
                attempt++;
                if (attempt > _maxRetries)
                {
                    throw new InvalidOperationException($"Operation '{operationName}' failed after {_maxRetries + 1} attempts.", ex);
                }

                int delayMs = CalculateBackoffDelay(attempt);

                Logger.LogWarning(
                    $"Transient error in '{operationName}': {ex.Message}. " +
                    $"Retrying in {delayMs}ms (attempt {attempt}/{_maxRetries})");

                cancellationToken.WaitHandle.WaitOne(delayMs);
            }
        }

        throw new InvalidOperationException($"Operation '{operationName}' failed after {_maxRetries + 1} attempts.");
    }

    public void ExecuteWithRetry(Action operation, string operationName = "Unknown")
    {
        ArgumentNullException.ThrowIfNull(operation);

        int attempt = 0;

        while (attempt <= _maxRetries)
        {
            try
            {
                if (attempt > 0)
                {
                    Logger.LogWarning($"Retry attempt {attempt} of {_maxRetries} for operation '{operationName}'");
                }

                operation();
                return;
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt <= _maxRetries)
            {
                attempt++;
                if (attempt > _maxRetries)
                {
                    throw new InvalidOperationException($"Operation '{operationName}' failed after {_maxRetries + 1} attempts.", ex);
                }

                int delayMs = CalculateBackoffDelay(attempt);

                Logger.LogWarning(
                    $"Transient error in '{operationName}': {ex.Message}. " +
                    $"Retrying in {delayMs}ms (attempt {attempt}/{_maxRetries})");

                Thread.Sleep(delayMs);
            }
        }

        throw new InvalidOperationException($"Operation '{operationName}' failed after {_maxRetries + 1} attempts.");
    }

    public static bool IsTransientError(Exception ex)
    {
        if (ex == null)
        {
            return false;
        }

        if (ex is ServerException serverEx)
        {
            return serverEx.ServerErrorCode == -2147024891 ||
                   serverEx.ServerErrorCode == -2130575339 ||
                   serverEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   serverEx.Message.Contains("throttl", StringComparison.OrdinalIgnoreCase) ||
                   serverEx.Message.Contains("service unavailable", StringComparison.OrdinalIgnoreCase);
        }

        if (ex is WebException outerWebEx)
        {
            if (IsWebExceptionTransient(outerWebEx))
            {
                return true;
            }
        }

        if (ex.InnerException is WebException webEx)
        {
            var response = webEx.Response as HttpWebResponse;
            if (response != null)
            {
                return response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                       response.StatusCode == HttpStatusCode.GatewayTimeout ||
                       response.StatusCode == HttpStatusCode.RequestTimeout ||
                       (int)response.StatusCode == 429;
            }
        }

        if (ex is TimeoutException)
        {
            return true;
        }

        if (ex is TaskCanceledException)
        {
            return true;
        }

        if (ex is OperationCanceledException)
        {
            return false;
        }

        if (ex.InnerException != null)
        {
            return IsTransientError(ex.InnerException);
        }

        return false;
    }

    private static bool IsWebExceptionTransient(WebException webEx)
    {
        var response = webEx.Response as HttpWebResponse;
        if (response != null)
        {
            return response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                   response.StatusCode == HttpStatusCode.GatewayTimeout ||
                   response.StatusCode == HttpStatusCode.RequestTimeout ||
                   (int)response.StatusCode == 429;
        }
        return false;
    }

    private int CalculateBackoffDelay(int attempt)
    {
        int exponentialDelay = _initialDelayMs * (int)Math.Pow(2, attempt - 1);

        int jitter = (int)(exponentialDelay * 0.1 * (Random.Shared.NextDouble() - 0.5) * 2);

        return Math.Max(100, exponentialDelay + jitter);
    }

    public override string ToString()
    {
        return $"RetryPolicy [MaxRetries: {_maxRetries}, InitialDelay: {_initialDelayMs}ms, " +
               $"MaxBackoff: {CalculateBackoffDelay(_maxRetries)}ms]";
    }
}
