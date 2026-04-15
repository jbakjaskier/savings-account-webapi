using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace Westpac.Evaluation.SavingsAccountCreator.Models;

public enum AccountType
{
    Savings,
    Checking
}

public record CreateCustomerRequest
{
    public string? CustomerNumber { get; init; }

    public CustomerName? CustomerName { get; init; }

    public static OpenApiSchema OpenApiSchema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["customerNumber"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "The customer number shared across channels including IB, Mobile etc",
                Example = "123456789",
                Pattern = @"^\d{1,19}$"
            },
            ["customerName"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Description = "The name of the customer",
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["firstName"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Description = "The first name of the customer",
                        Example = "John",
                        MinLength = 3,
                        MaxLength = 100
                    },
                    ["lastName"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Description = "The last name of the customer",
                        Example = "Smith",
                        MinLength = 3,
                        MaxLength = 100
                    }
                },
                Required = new HashSet<string>
                {
                    "firstName",
                    "lastName"
                }
            }
        },
        Example = new JsonObject
        {
            ["customerNumber"] = JsonValue.Create("123456789"),
            ["customerName"] = new JsonObject
            {
                ["firstName"] = JsonValue.Create("John"),
                ["lastName"] = JsonValue.Create("Smith")
            }
        },
        Required = new HashSet<string>
        {
            "customerNumber",
            "customerName"
        }
    };
}

public record CreateAccountRequest
{
    public string? BranchCode { get; init; }

    public string? AccountType { get; init; }

    public string? CustomerNumber { get; init; }

    public string? AccountNickName { get; init; }


    public static OpenApiSchema OpenApiSchema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["branchCode"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Example = "0000",
                Description = "The branch code of the account. This is a 4 digit number",
                Pattern = @"^\d{4}$"
            },
            ["accountType"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "The type of account to create",
                Example = nameof(Models.AccountType.Savings),
                Enum = new List<JsonNode>
                {
                    JsonValue.Create(nameof(Models.AccountType.Savings)),
                    JsonValue.Create(nameof(Models.AccountType.Checking))
                }
            },
            ["customerNumber"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Example = "123456789",
                Description =
                    "The customer number of the account. This is the customer number that is shared to the customer and shared across channels including IB, Mobile etc",
                Pattern = @"^\d{1,19}$"
            },
            ["accountNickName"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Example = "My Savings Account",
                Description =
                    "The nickname of the account. This is the nick name of the account that is set by the customer",
                Pattern = @"^[a-zA-Z0-9\s]+$",
                MinLength = 5,
                MaxLength = 30
            }
        },
        Example = new JsonObject
        {
            ["branchCode"] = JsonValue.Create("0000"),
            ["accountType"] = JsonValue.Create(nameof(Models.AccountType.Savings)),
            ["customerNumber"] = JsonValue.Create("123456789"),
            ["accountNickName"] = JsonValue.Create("My Savings Account")
        },
        Required = new HashSet<string>
        {
            "branchCode",
            "accountType",
            "customerNumber"
        }
    };
}

public record CustomerName
{
    public string? FirstName { get; init; }

    public string? LastName { get; init; }
}