using Microsoft.Extensions.Options;
using SPO2SQL.Configuration;

namespace SPO2SQL;

internal sealed class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var builder = CreateHostBuilder(args);
            using var host = builder.Build();

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

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                var env = context.HostingEnvironment;

                config
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                    .AddUserSecrets<Program>(optional: true)
                    .AddEnvironmentVariables()
                    .AddEnvironmentVariables(prefix: "SPO2SQL_");

                config.AddCommandLine(args);
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();

                var appOptions = context.Configuration.GetSection(ApplicationOptions.SectionName).Get<ApplicationOptions>();
                if (appOptions != null)
                {
                    logging.SetMinimumLevel(appOptions.LogLevel);
                }
            })
            .ConfigureServices((context, services) =>
            {
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

                services.AddHttpClient();
                services.AddHostedService<Application>();
            });
}
