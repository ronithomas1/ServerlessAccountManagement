using Amazon.DynamoDBv2.Model;
using Amazon.DynamoDBv2;
using Aspire.Hosting.AWS.Lambda;
using Amazon.Lambda;
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache").WithRedisCommander(); 

#region DynamoDB Setup
var dynamoDbLocal = builder.AddAWSDynamoDBLocal("DynamoDBAccounts");

// Seed the DynamoDB local instance once the resource is ready.
builder.Eventing.Subscribe<ResourceReadyEvent>(dynamoDbLocal.Resource, async (evnt, ct) =>
{
    // Configure DynamoDB service client to connect to DynamoDB local
    var serviceUrl = dynamoDbLocal.Resource.GetEndpoint("http").Url;
    var ddbClient = new AmazonDynamoDBClient(new AmazonDynamoDBConfig { ServiceURL = serviceUrl });

    // Create Accounts table
    await ddbClient.CreateTableAsync(new CreateTableRequest
    {
        TableName = "Accounts",
        AttributeDefinitions = new List<AttributeDefinition>
        {
            new AttributeDefinition { AttributeName = "Id", AttributeType = "S" }
        },
        KeySchema = new List<KeySchemaElement>
        {
            new KeySchemaElement { AttributeName = "Id", KeyType = "HASH" }
        },
        BillingMode = BillingMode.PAY_PER_REQUEST
    });

    // Add an account to the Accounts table.
    await ddbClient.PutItemAsync(new PutItemRequest
    {
        TableName = "Accounts",
        Item = new Dictionary<string, AttributeValue>
        {
            { "Id", new AttributeValue("1") },
            { "Name", new AttributeValue("Amazon") },
            { "Address", new AttributeValue("Seattle, WA") }
        }
    });
});
#endregion

#region Lambda Functions
#pragma warning disable CA2252 // Opt-in for preview features.

var defaultRouteFunction = builder.AddAWSLambdaFunction<Projects.ServerlessAccountManagement>(
                                    name: "DefaultRouteFunction",
                                    lambdaHandler: "ServerlessAccountManagement::ServerlessAccountManagement.Functions_Default_Generated::Default")
                                .WithReference(dynamoDbLocal)
                                .WaitFor(dynamoDbLocal)
                                .WithReference(cache)
                                .WaitFor(cache);

var getAccountFunction = builder.AddAWSLambdaFunction<Projects.ServerlessAccountManagement>(
                                    name: "GetAccountFunction",
                                    lambdaHandler: "ServerlessAccountManagement::ServerlessAccountManagement.Functions_GetAccountAsync_Generated::GetAccountAsync",
                                    options: new LambdaFunctionOptions
                                    {
                                        ApplicationLogLevel = ApplicationLogLevel.DEBUG
                                    })
                                .WithReference(dynamoDbLocal)
                                .WaitFor(dynamoDbLocal)
                                .WithReference(cache)
                                .WaitFor(cache);

var listAccountsFunction = builder.AddAWSLambdaFunction<Projects.ServerlessAccountManagement>(
                                    name: "ListAccountsFunction",
                                    lambdaHandler: "ServerlessAccountManagement::ServerlessAccountManagement.Functions_ListAccountsAsync_Generated::ListAccountsAsync",
                                    options: new LambdaFunctionOptions
                                    {
                                        ApplicationLogLevel = ApplicationLogLevel.DEBUG
                                    })
                                .WithReference(dynamoDbLocal)
                                .WaitFor(dynamoDbLocal)
                                .WithReference(cache)
                                .WaitFor(cache);

var postAccountFunction = builder.AddAWSLambdaFunction<Projects.ServerlessAccountManagement>(
                                    name: "PostAccountFunction",
                                    lambdaHandler: "ServerlessAccountManagement::ServerlessAccountManagement.Functions_PostAccountAsync_Generated::PostAccountAsync",
                                    options: new LambdaFunctionOptions
                                    {
                                        ApplicationLogLevel = ApplicationLogLevel.DEBUG
                                    })
                                .WithReference(dynamoDbLocal)
                                .WaitFor(dynamoDbLocal)
                                .WithReference(cache)
                                .WaitFor(cache);
#endregion

#region Exposing Lambda Functions
var apiGateway = builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", APIGatewayType.Rest, new APIGatewayEmulatorOptions {Port = 9925})
        .WithReference(defaultRouteFunction, Method.Get, "/")
        .WithReference(getAccountFunction, Method.Get, "/account/{id}")
        .WithReference(listAccountsFunction, Method.Get, "/account")
        .WithReference(postAccountFunction, Method.Post, "/account");
#endregion

builder.Build().Run();
