using Microsoft.Data.SqlClient;
using Microsoft.SharePoint.Client;
using SPO2SQL.Logging;

namespace SPO2SQL.SharePoint;

public class HealthChecker
{
    private readonly int _verbosity;

    public class HealthCheckResult
    {
        public bool IsHealthy { get; set; } = true;
        public List<string> Warnings { get; set; } = [];
        public List<string> Errors { get; set; } = [];

        public override string ToString()
        {
            var status = IsHealthy ? "HEALTHY" : "UNHEALTHY";
            var result = $"Health Status: {status}";

            if (Errors.Count > 0)
            {
                result += $"\nErrors ({Errors.Count}):";
                result = Errors.Aggregate(result, (current, error) => current + $"\n  ✗ {error}");
            }

            if (Warnings.Count > 0)
            {
                result += $"\nWarnings ({Warnings.Count}):";
                result = Warnings.Aggregate(result, (current, warning) => current + $"\n  ⚠ {warning}");
            }

            return result;
        }
    }

    public HealthChecker(int verbosity = 0)
    {
        _verbosity = verbosity;
    }

    public async Task<HealthCheckResult> PerformHealthCheckAsync(
        SPOUser spoUser,
        string sharePointUrl,
        string sqlConnectionString,
        CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult();

        if (_verbosity >= 2)
        {
            Logger.Log(2, "Starting health checks...");
        }

        ValidateSPOUserCredentials(spoUser, result);

        await ValidateSharePointConnectivityAsync(spoUser, sharePointUrl, result, cancellationToken)
            .ConfigureAwait(false);

        await ValidateSqlConnectivityAsync(sqlConnectionString, result, cancellationToken)
            .ConfigureAwait(false);

        result.IsHealthy = result.Errors.Count == 0;

        if (_verbosity >= 1)
        {
            Logger.Log(1, result.ToString());
        }

        return result;
    }

    private static void ValidateSPOUserCredentials(SPOUser? spoUser, HealthCheckResult result)
    {
        if (spoUser == null)
        {
            result.Errors.Add("SPOUser is null - SharePoint credentials not initialized.");
            return;
        }

        if (string.IsNullOrWhiteSpace(spoUser.Username))
        {
            result.Errors.Add("SharePoint username is empty or null.");
        }
    }

    private async Task ValidateSharePointConnectivityAsync(
        SPOUser spoUser,
        string sharePointUrl,
        HealthCheckResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sharePointUrl))
            {
                result.Errors.Add("SharePoint URL is not configured.");
                return;
            }

            if (!sharePointUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !sharePointUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add("SharePoint URL must start with http:// or https://");
                return;
            }

            using var clientContext = new ClientContext(sharePointUrl);
            spoUser.ApplyAuthentication(clientContext, sharePointUrl);

            var web = clientContext.Web;
            clientContext.Load(web, w => w.Title);

            try
            {
                await clientContext.ExecuteQueryWithCancellationAsync(cancellationToken).ConfigureAwait(false);
                if (_verbosity >= 3)
                {
                    Logger.Log(3, $"✓ SharePoint connectivity verified. Site: {web.Title}");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"SharePoint connection failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"SharePoint validation error: {ex.Message}");
        }
    }

    private async Task ValidateSqlConnectivityAsync(
        string sqlConnectionString,
        HealthCheckResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sqlConnectionString))
            {
                result.Warnings.Add("SQL connection string is empty. SQL operations will fail if attempted.");
                return;
            }

            await using var connection = new SqlConnection(sqlConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (_verbosity >= 3)
            {
                Logger.Log(3, "✓ SQL Server connectivity verified.");
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

    public async Task<HealthCheckResult> ValidateListAccessAsync(
        SPOUser spoUser,
        string sharePointUrl,
        string listName,
        CancellationToken cancellationToken = default)
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

            using var clientContext = new ClientContext(sharePointUrl);
            spoUser?.ApplyAuthentication(clientContext, sharePointUrl);

            var list = clientContext.Web.Lists.GetByTitle(listName);
            clientContext.Load(list, l => l.Title, l => l.ItemCount);

            try
            {
                await clientContext.ExecuteQueryWithCancellationAsync(cancellationToken).ConfigureAwait(false);
                if (_verbosity >= 3)
                {
                    Logger.Log(3, $"✓ List '{listName}' is accessible ({list.ItemCount} items)");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Cannot access list '{listName}': {ex.Message}");
                result.IsHealthy = false;
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
