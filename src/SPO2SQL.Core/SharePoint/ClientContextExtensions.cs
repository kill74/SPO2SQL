using Microsoft.SharePoint.Client;

namespace SPO2SQL.SharePoint;

/// <summary>
/// Adds cooperative cancellation to the CSOM query API, which only exposes a
/// parameterless async overload in the current SharePoint client package.
/// </summary>
internal static class ClientContextExtensions
{
    public static async Task ExecuteQueryWithCancellationAsync(
        this ClientRuntimeContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await context.ExecuteQueryAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
