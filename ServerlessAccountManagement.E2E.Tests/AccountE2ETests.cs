using Amazon.Lambda;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Model;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace ServerlessAccountManagement.E2E.Tests;

public class AccountE2ETests
{
    [Fact]
    public async Task GetAccountThroughApiGateway()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.ServerlessAccountManagement_AppHost>();
        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotificationService
            .WaitForResourceAsync("GetAccountFunction", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(120));

        using var client = app.CreateHttpClient("APIGatewayEmulator");

        var json = await client.GetStringAsync("/account/1");
        Assert.NotNull(json);
    }

    [Fact]
    public async Task GetAccountThroughLambdaSdk()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.ServerlessAccountManagement_AppHost>();
        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotificationService
            .WaitForResourceAsync("GetAccountFunction", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(120));

        using var lambdaEndpointClient = app.CreateHttpClient("LambdaServiceEmulator");

        var lambdaConfig = new AmazonLambdaConfig
        {
            ServiceURL = lambdaEndpointClient.BaseAddress!.ToString()
        };
        var lambdaClient = new AmazonLambdaClient(lambdaConfig);

        var invokeRequest = new InvokeRequest
        {
            FunctionName = "GetAccountFunction",
            Payload = CreateGetRequest("1")
        };

        var invokeResponse = await lambdaClient.InvokeAsync(invokeRequest);

        var apiGatewayResponse = JsonSerializer.Deserialize<APIGatewayProxyResponse>(invokeResponse.Payload);
        Assert.NotNull(apiGatewayResponse);
        Assert.Equal(200, apiGatewayResponse.StatusCode);
        Assert.NotNull(apiGatewayResponse.Body);
    }


    private string CreateGetRequest(string accountId)
    {
        var request = new APIGatewayProxyRequest
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                HttpMethod = "GET",
                Path = $"/account/{accountId}"
            },
            PathParameters = new Dictionary<string, string>
            {
                { "id", accountId }
            }
        };

        return JsonSerializer.Serialize(request);
    }
}