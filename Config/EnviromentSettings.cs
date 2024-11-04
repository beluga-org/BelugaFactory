using Microsoft.Extensions.Configuration;

namespace BelugaFactory.Config;
public static class EnvironmentSettings
{
    public static string OpenAiApiKey { get; private set; }
    public static string AzureStorageConnectionString { get; private set; }
    public static string AzureEventsHubConnectionString { get; private set; }

    static EnvironmentSettings()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        var configuration = builder.Build();

        OpenAiApiKey = configuration["ApiKeys:OpenAiApiKey"]; 
        AzureStorageConnectionString = configuration["ConnectionStrings:AzureWebJobsStorage"];
        AzureEventsHubConnectionString = configuration["ConnectionStrings:AzureEventsHubs"];
    }
}

