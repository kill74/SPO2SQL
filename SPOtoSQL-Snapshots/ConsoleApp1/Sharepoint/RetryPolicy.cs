using System;
using System.Net;
using System.Threading;
using Microsoft.SharePoint.Client;
using Bring.SPODataQuality;

namespace Bring.Sharepoint
{
  /// <summary>
  /// Provides retry logic with exponential backoff for resilient SharePoint operations.
  /// Handles transient failures automatically.
  /// </summary>
  public class RetryPolicy
  {
    private readonly int _maxRetries;
    private readonly int _initialDelayMs;

    /// <summary>
    /// Creates a new retry policy with default settings.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3).</param>
    /// <param name="initialDelayMs">Initial delay in milliseconds before first retry (default: 1000).</param>
    public RetryPolicy(int maxRetries = 3, int initialDelayMs = 1000)
    {
      if (maxRetries < 1)
        throw new ArgumentException("Maximum retries must be at least 1.", nameof(maxRetries));

      if (initialDelayMs < 100)
        throw new ArgumentException("Initial delay must be at least 100ms.", nameof(initialDelayMs));

      _maxRetries = maxRetries;
      _initialDelayMs = initialDelayMs;
    }

    /// <summary>
    /// Executes an operation with automatic retry on transient failures.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="operationName">Name of the operation for logging.</param>
    /// <returns>The result of the operation.</returns>
    public T ExecuteWithRetry<T>(Func<T> operation, string operationName = "Unknown")
    {
      if (operation == null)
        throw new ArgumentNullException(nameof(operation));

      int attempt = 0;

      while (attempt <= _maxRetries)
      {
        try
        {
          if (attempt > 0)
          {
            _logger.LogWarning($"Retry attempt {attempt} of {_maxRetries} for operation '{operationName}'");
          }

          return operation();
        }
        catch (Exception ex) when (IsTransientError(ex) && attempt < _maxRetries)
        {
          attempt++;
          int delayMs = CalculateBackoffDelay(attempt);

          _logger.LogWarning(
              $"Transient error in '{operationName}': {ex.Message}. " +
              $"Retrying in {delayMs}ms (attempt {attempt}/{_maxRetries})");

          Thread.Sleep(delayMs);
        }
      }

      // If we've exhausted retries, make one final attempt (will throw if it fails)
      return operation();
    }

    /// <summary>
    /// Executes an asynchronous operation with automatic retry on transient failures.
    /// </summary>
    public void ExecuteWithRetry(Action operation, string operationName = "Unknown")
    {
      if (operation == null)
        throw new ArgumentNullException(nameof(operation));

      int attempt = 0;

      while (attempt <= _maxRetries)
      {
        try
        {
          if (attempt > 0)
          {
            _logger.LogWarning($"Retry attempt {attempt} of {_maxRetries} for operation '{operationName}'");
          }

          operation();
          return;
        }
        catch (Exception ex) when (IsTransientError(ex) && attempt < _maxRetries)
        {
          attempt++;
          int delayMs = CalculateBackoffDelay(attempt);

          _logger.LogWarning(
              $"Transient error in '{operationName}': {ex.Message}. " +
              $"Retrying in {delayMs}ms (attempt {attempt}/{_maxRetries})");

          Thread.Sleep(delayMs);
        }
      }

      // If we've exhausted retries, make one final attempt (will throw if it fails)
      operation();
    }

    /// <summary>
    /// Determines if an exception represents a transient error that can be retried.
    /// </summary>
    /// <param name="ex">The exception to evaluate.</param>
    /// <returns>True if the error is transient and can be retried; false otherwise.</returns>
    public static bool IsTransientError(Exception ex)
    {
      if (ex == null)
        return false;

      // SharePoint CSOM specific transient errors
      if (ex is ServerException serverEx)
      {
        // 503 Service Unavailable (throttling)
        // 500 Internal Server Error (transient)
        // 429 Too Many Requests (throttling)
        return serverEx.ServerErrorCode == -2147024891 || // Resource not found - transient
               serverEx.ServerErrorCode == -2130575339 || // Service temporarily unavailable
               serverEx.Message.Contains("timeout") ||
               serverEx.Message.Contains("throttl") ||
               serverEx.Message.Contains("service unavailable");
      }

      // HTTP status codes that are transient
      if (ex.InnerException is WebException webEx)
      {
        var response = webEx.Response as HttpWebResponse;
        if (response != null)
        {
          return response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                 response.StatusCode == HttpStatusCode.GatewayTimeout ||
                 response.StatusCode == HttpStatusCode.RequestTimeout ||
                 (int)response.StatusCode == 429; // Too Many Requests
        }
      }

      // Generic transient indicators
      if (ex is TimeoutException ||
          ex is TaskCanceledException ||
          ex is OperationCanceledException)
      {
        return true;
      }

      // Check inner exception recursively
      if (ex.InnerException != null)
      {
        return IsTransientError(ex.InnerException);
      }

      return false;
    }

    /// <summary>
    /// Calculates exponential backoff delay based on attempt number.
    /// Formula: initialDelay * (2 ^ (attempt - 1))
    /// Example: 1000ms * 2^0 = 1000ms, 1000ms * 2^1 = 2000ms, 1000ms * 2^2 = 4000ms
    /// </summary>
    private int CalculateBackoffDelay(int attempt)
    {
      int exponentialDelay = _initialDelayMs * (int)Math.Pow(2, attempt - 1);

      // Add jitter (±10%) to prevent thundering herd
      int jitter = (int)(exponentialDelay * 0.1 * (new Random().NextDouble() - 0.5) * 2);

      return Math.Max(100, exponentialDelay + jitter); // Minimum 100ms
    }

    /// <summary>
    /// Gets the retry policy description.
    /// </summary>
    public override string ToString()
    {
      return $"RetryPolicy [MaxRetries: {_maxRetries}, InitialDelay: {_initialDelayMs}ms, " +
             $"MaxBackoff: {CalculateBackoffDelay(_maxRetries)}ms]";
    }
  }
}
