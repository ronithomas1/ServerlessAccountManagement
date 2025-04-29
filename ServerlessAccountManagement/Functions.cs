using Amazon.Lambda.Core;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.AWSLambda;
using Amazon.DynamoDBv2.DataModel;
using StackExchange.Redis;
using System.Text.Json;
using ServerlessAccountManagement.Model;
using Microsoft.AspNetCore.Http.HttpResults;
using Amazon.Lambda.APIGatewayEvents;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ServerlessAccountManagement;

public class Functions
{
    private TracerProvider _traceProvider;
    private DynamoDBContext _ddbContext;
    private IDatabase _redis;

    public Functions(TracerProvider traceProvider, DynamoDBContext ddbContext, IConnectionMultiplexer connectionMultiplexer)
    {
        _traceProvider = traceProvider;
        _ddbContext = ddbContext;
        _redis = connectionMultiplexer.GetDatabase();
    }

    [LambdaFunction]
    [RestApi(LambdaHttpMethod.Get, "/")]
    public IHttpResult Default(ILambdaContext context)
    {
        return HttpResults.Ok("Default route for the account management REST API. Try navigating to /account/1 to get the seeded account.");
    }


    [LambdaFunction]
    [RestApi(LambdaHttpMethod.Get, "/account/{id}")]
    public Task<IHttpResult> GetAccountAsync(string id, ILambdaContext context)
        => AWSLambdaWrapper.TraceAsync(_traceProvider, async (id, context) =>
        {
            context.Logger.LogInformation("Attempting to load account {id}", id);

            Accounts? account = null;
            var accountJson = _redis.StringGet(id);

            if (!accountJson.IsNull)
            {
                context.Logger.LogDebug("Loaded account from redis cache");
                account = JsonSerializer.Deserialize<Accounts>(accountJson.ToString());

                if (account == null)
                {
                    context.Logger.LogWarning("Cached account data from Redis failed to deserialize to Account type");
                }
            }
            
            if (account == null)
            {
                context.Logger.LogDebug("Loaded account from DynamoDB");
                account = await _ddbContext.LoadAsync<Accounts>(id);
                accountJson = JsonSerializer.Serialize(account);

                if (_redis.StringSet(id, JsonSerializer.Serialize(account)))
                {
                    context.Logger.LogDebug("Saved account {id} to redis cache", id);
                }
            }

            return HttpResults.Ok(account);
        }, id, context);

    [LambdaFunction]
    [RestApi(LambdaHttpMethod.Post, "/account/")]
    public Task<IHttpResult> PostAccountAsync([FromBody] Accounts account, ILambdaContext context)
        => AWSLambdaWrapper.TraceAsync(_traceProvider, async (id, context) =>
        {
            if (string.IsNullOrEmpty(account.Name))
            {
                return HttpResults.BadRequest("Required account name is missing");
            }

            account.Id = Guid.NewGuid().ToString();
            context.Logger.LogDebug("Attempting to save account {name}", account.Name);

            await _ddbContext.SaveAsync(account);
            context.Logger.LogInformation("Saved account {name} with id {id}", account.Name, account.Id);

            return HttpResults.Ok(account.Id);

        }, account, context);

    [LambdaFunction]
    [RestApi(LambdaHttpMethod.Get, "/account/")]
    public Task<IHttpResult> ListAccountsAsync(ILambdaContext context)
        => AWSLambdaWrapper.TraceAsync<string, IHttpResult>(_traceProvider, async (_, context) =>
        {
            context.Logger.LogDebug("Listing all accounts");

            var accounts = await _ddbContext.ScanAsync<Accounts>(new ScanCondition[0]).GetRemainingAsync();

            context.Logger.LogDebug("{count} accounts listed", accounts.Count);
            return HttpResults.Ok(accounts);

        }, string.Empty, context);
}
