using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Microsoft.SharePoint.Client;

namespace Bring.Sharepoint
{
  /// <summary>
  /// Performs health checks and validation on SharePoint and SQL configurations.
  /// </summary>
  public class HealthChecker
  {
    private readonly Logger _logger;
    private readonly int _verbosity;

    /// <summary>
    /// Represents the result of a health check.
    /// </summary>
    public class HealthCheckResult
    {
      public bool IsHealthy { get; set; } = true;
      public List<string> Warnings { get; set; } = new List<string>();
      public List<string> Errors { get; set; } = new List<string>();

      public override string ToString()
      {
        var status = IsHealthy ? "HEALTHY" : "UNHEALTHY";
        var result = $"Health Status: {status}";

        if (Errors.Count > 0)
        {
          result += $"\nErrors ({Errors.Count}):";
          foreach (var error in Errors)
            result += $"\n  ✗ {error}";
        }

        if (Warnings.Count > 0)
        {
          result += $"\nWarnings ({Warnings.Count}):";
          foreach (var warning in Warnings)
            result += $"\n  ⚠ {warning}";
        }

        return result;
      }
    }

    public HealthChecker(Logger logger = null, int verbosity = 0)
    {
      _logger = logger ?? Logger.Instance;
      _verbosity = verbosity;
    }

    /// <summary>
    /// Performs comprehensive health checks on the SharePoint and SQL configuration.
    /// </summary>
    public HealthCheckResult PerformHealthCheck(SPOUser spoUser, string sqlConnectionString)
    {
      var result = new HealthCheckResult();

      if (_verbosity >= 2)
        _logger.LogWarning("Starting health checks...");

      // Check SPOUser credentials
      ValidateSPOUserCredentials(spoUser, result);

      // Check SharePoint connectivity
      ValidateSharePointConnectivity(spoUser, result);

      // Check SQL connectivity
      ValidateSqlConnectivity(sqlConnectionString, result);

      // Determine overall health
      result.IsHealthy = result.Errors.Count == 0;

      if (_verbosity >= 1)
        _logger.LogWarning(result.ToString());

      return result;
    }

    /// <summary>
    /// Validates SPOUser credentials are properly configured.
    /// </summary>
    private void ValidateSPOUserCredentials(SPOUser spoUser, HealthCheckResult result)
    {
      if (spoUser == null)
      {
        result.Errors.Add("SPOUser is null - SharePoint credentials not initialized.");
        result.IsHealthy = false;
        return;
      }

      if (string.IsNullOrWhiteSpace(spoUser.UserName))
      {
        result.Errors.Add("SharePoint username is empty or null.");
        result.IsHealthy = false;
      }

      if (spoUser.Password == null || spoUser.Password.Length == 0)
      {
        result.Errors.Add("SharePoint password is empty or null.");
        result.IsHealthy = false;
      }
    }

    /// <summary>
    /// Validates connectivity to SharePoint Online.
    /// </summary>
    private void ValidateSharePointConnectivity(SPOUser spoUser, HealthCheckResult result)
    {
      try
      {
        if (spoUser == null || string.IsNullOrWhiteSpace(spoUser.SharePointURL))
        {
          result.Errors.Add("SharePoint URL is not configured.");
          return;
        }

        // Verify URL format
        if (!spoUser.SharePointURL.StartsWith("https://") && !spoUser.SharePointURL.StartsWith("http://"))
        {
          result.Errors.Add("SharePoint URL must start with http:// or https://");
          return;
        }

        // Attempt to create client context (validates credentials and connectivity)
        using (var clientContext = spoUser.GetClientContext())
        {
          if (clientContext == null)
          {
            result.Errors.Add("Failed to create SharePoint client context.");
            return;
          }

          // Quick validation: load web title
          var web = clientContext.Web;
          clientContext.Load(web, w => w.Title);

          try
          {
            clientContext.ExecuteQuery();
            if (_verbosity >= 3)
              _logger.LogWarning($"✓ SharePoint connectivity verified. Site: {web.Title}");
          }
          catch (Exception ex)
          {
            result.Errors.Add($"SharePoint connection failed: {ex.Message}");
          }
        }
      }
      catch (Exception ex)
      {
        result.Errors.Add($"SharePoint validation error: {ex.Message}");
      }
    }

    /// <summary>
    /// Validates connectivity to SQL Server.
    /// </summary>
    private void ValidateSqlConnectivity(string sqlConnectionString, HealthCheckResult result)
    {
      try
      {
        if (string.IsNullOrWhiteSpace(sqlConnectionString))
        {
          result.Warnings.Add("SQL connection string is empty. SQL operations will fail if attempted.");
          return;
        }

        using (var connection = new SqlConnection(sqlConnectionString))
        {
          connection.Open();
          if (_verbosity >= 3)
            _logger.LogWarning("✓ SQL Server connectivity verified.");
          connection.Close();
        }
      }
      catch (SqlException sqlEx)
      {
        result.Errors.Add($"SQL connectivity failed: {sqlEx.Message}");
      }
      catch (Exception ex)
      {
        result.Errors.Add($"SQL validation error: {ex.Message}");
      }
    }

    /// <summary>
    /// Validates that a specific SharePoint list exists and is accessible.
    /// </summary>
    public HealthCheckResult ValidateListAccess(SPOUser spoUser, string listName)
    {
      var result = new HealthCheckResult();

      try
      {
        if (string.IsNullOrWhiteSpace(listName))
        {
          result.Errors.Add("List name cannot be empty.");
          result.IsHealthy = false;
          return result;
        }

        using (var clientContext = spoUser.GetClientContext())
        {
          var list = clientContext.Web.Lists.GetByTitle(listName);
          clientContext.Load(list, l => l.Title, l => l.ItemCount);

          try
          {
            clientContext.ExecuteQuery();
            if (_verbosity >= 3)
              _logger.LogWarning($"✓ List '{listName}' is accessible ({list.ItemCount} items)");
          }
          catch (Exception ex)
          {
            result.Errors.Add($"Cannot access list '{listName}': {ex.Message}");
            result.IsHealthy = false;
          }
        }
      }
      catch (Exception ex)
      {
        result.Errors.Add($"List validation error: {ex.Message}");
        result.IsHealthy = false;
      }

      return result;
    }
  }
}
