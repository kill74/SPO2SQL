using Bring.Configuration;
using Microsoft.Extensions.Options;

namespace Bring;

/// <summary>
/// Main entry point for the SharePoint Sync Tool.
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var builder = CreateHostBuilder(args);
            var host = builder.Build();

            // Run the application
            await host.RunAsync();
            
            return 0;
        }
        catch (OptionsValidationException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Configuration validation failed:");
            foreach (var failure in ex.Failures)
            {
                Console.WriteLine($"  - {failure}");
            }
            Console.ResetColor();
            return 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            return 1;
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                // Clear default configuration sources
                config.Sources.Clear();

                var env = context.HostingEnvironment;

                // Add configuration sources in order of precedence (lowest to highest)
                config
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                    .AddUserSecrets<Program>(optional: true) // For local development
                    .AddEnvironmentVariables(prefix: "SPO2SQL_"); // For production

                // Support legacy XML config if needed (optional fallback)
                // Custom XML configuration provider could be added here

                // Command-line arguments override everything
                config.AddCommandLine(args);
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();
                
                // Apply log level from configuration
                var appOptions = context.Configuration.GetSection(ApplicationOptions.SectionName).Get<ApplicationOptions>();
                if (appOptions != null)
                {
                    logging.SetMinimumLevel(appOptions.LogLevel);
                }
            })
            .ConfigureServices((context, services) =>
            {
                // Register configuration options with validation
                services.AddOptions<ApplicationOptions>()
                    .Bind(context.Configuration.GetSection(ApplicationOptions.SectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                services.AddOptions<SharePointOptions>()
                    .Bind(context.Configuration.GetSection(SharePointOptions.SectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                services.AddOptions<SqlOptions>()
                    .Bind(context.Configuration.GetSection(SqlOptions.SectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                // Register application services (to be added)
                // services.AddSingleton<ISharePointService, SharePointService>();
                // services.AddSingleton<ISqlService, SqlService>();
                // services.AddTransient<IDataQualityService, DataQualityService>();

                // Register the main application as a hosted service
                services.AddHostedService<Application>();
            });
}
