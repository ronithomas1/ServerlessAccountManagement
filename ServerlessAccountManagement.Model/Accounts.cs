using Amazon.DynamoDBv2.DataModel;

namespace ServerlessAccountManagement.Model;

[DynamoDBTable("Accounts")]
public class Accounts
{
    [DynamoDBHashKey("Id")]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Address { get; set; }
}
