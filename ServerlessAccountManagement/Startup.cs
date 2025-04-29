using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ServerlessAccountManagement;

[LambdaStartup]
public class Startup
{
    public HostApplicationBuilder ConfigureHostBuilder()
    {
        var hostBuilder = new HostApplicationBuilder();

        // Add other configuration if needed
        hostBuilder.AddServiceDefaults();

        hostBuilder.AddRedisClient(connectionName: "cache");
        hostBuilder.Services.AddAWSService<IAmazonDynamoDB>();

        hostBuilder.Services.AddSingleton<DynamoDBContext>(sp =>
        {
            return new DynamoDBContext(sp.GetRequiredService<IAmazonDynamoDB>(), new DynamoDBContextConfig
            {
                DisableFetchingTableMetadata = true
            });
        });

        return hostBuilder;
    }
}
