using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Microsoft.SharePoint.Client;
using Bring.SPODataQuality;

namespace Bring.Sharepoint
{
  public class HealthChecker
  {
    private readonly int _verbosity;

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

    public HealthChecker(int verbosity = 0)
    {
      _verbosity = verbosity;
    }

    public HealthCheckResult PerformHealthCheck(SPOUser spoUser, string sharePointUrl, string sqlConnectionString)
    {
      var result = new HealthCheckResult();

      if (_verbosity >= 2)
        Logger.Log(2, "Starting health checks...");

      ValidateSPOUserCredentials(spoUser, result);

      ValidateSharePointConnectivity(spoUser, sharePointUrl, result);

      ValidateSqlConnectivity(sqlConnectionString, result);

      result.IsHealthy = result.Errors.Count == 0;

      if (_verbosity >= 1)
        Logger.Log(1, result.ToString());

      return result;
    }

    private void ValidateSPOUserCredentials(SPOUser spoUser, HealthCheckResult result)
    {
      if (spoUser == null)
      {
        result.Errors.Add("SPOUser is null - SharePoint credentials not initialized.");
        result.IsHealthy = false;
        return;
      }

      if (string.IsNullOrWhiteSpace(spoUser.Username))
      {
        result.Errors.Add("SharePoint username is empty or null.");
        result.IsHealthy = false;
      }
    }

    private void ValidateSharePointConnectivity(SPOUser spoUser, string sharePointUrl, HealthCheckResult result)
    {
      try
      {
        if (spoUser == null)
        {
          result.Errors.Add("SPOUser is null - cannot validate connectivity.");
          return;
        }

        if (string.IsNullOrWhiteSpace(sharePointUrl))
        {
          result.Errors.Add("SharePoint URL is not configured.");
          return;
        }

        if (!sharePointUrl.StartsWith("https://") && !sharePointUrl.StartsWith("http://"))
        {
          result.Errors.Add("SharePoint URL must start with http:// or https://");
          return;
        }

        using (var clientContext = new ClientContext(sharePointUrl))
        {
          spoUser.ApplyAuthentication(clientContext, sharePointUrl);

          var web = clientContext.Web;
          clientContext.Load(web, w => w.Title);

          try
          {
            clientContext.ExecuteQuery();
            if (_verbosity >= 3)
              Logger.Log(3, $"✓ SharePoint connectivity verified. Site: {web.Title}");
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
            Logger.Log(3, "✓ SQL Server connectivity verified.");
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

    public HealthCheckResult ValidateListAccess(SPOUser spoUser, string sharePointUrl, string listName)
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

        if (string.IsNullOrWhiteSpace(sharePointUrl))
        {
          result.Errors.Add("SharePoint URL cannot be empty.");
          result.IsHealthy = false;
          return result;
        }

        using (var clientContext = new ClientContext(sharePointUrl))
        {
          if (spoUser != null)
            spoUser.ApplyAuthentication(clientContext, sharePointUrl);

          var list = clientContext.Web.Lists.GetByTitle(listName);
          clientContext.Load(list, l => l.Title, l => l.ItemCount);

          try
          {
            clientContext.ExecuteQuery();
            if (_verbosity >= 3)
              Logger.Log(3, $"✓ List '{listName}' is accessible ({list.ItemCount} items)");
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
