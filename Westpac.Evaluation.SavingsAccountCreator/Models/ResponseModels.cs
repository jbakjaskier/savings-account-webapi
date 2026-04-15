using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace Westpac.Evaluation.SavingsAccountCreator.Models;

public record CustomerResponse
{
    public required long CustomerNumber { get; init; }

    public static OpenApiSchema OpenApiSchema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["customerNumber"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Example = "123456789",
                Description =
                    "The customer number of the account. This is the customer number that is shared to the customer and shared across channels including IB, Mobile etc",
                Pattern = @"^\d{1,19}$"
            }
        },
        Example = new JsonObject
        {
            ["customerNumber"] = JsonValue.Create("123456789")
        },
        Required = new HashSet<string>
        {
            "customerNumber"
        }
    };
}

public record AccountResponse
{
    /// <summary>
    ///     This is the bank code for the account.
    ///     For Westpac this is a static value of 03
    /// </summary>
    public required string BankCode { get; init; }

    /// <summary>
    ///     This is the account number for the account.
    ///     This is a seven digit value
    /// </summary>
    public required string AccountNumber { get; init; }

    /// <summary>
    ///     This is a four digit value assigned based on the branch that the account is opened in within Westpac
    /// </summary>
    public required string BranchCode { get; init; }

    /// <summary>
    ///     This is a three digit value that is appended to the account number based on the number of accounts opened by the
    ///     customer
    /// </summary>
    public required string AccountSuffix { get; init; }

    public static OpenApiSchema OpenApiSchema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["bankCode"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Example = "03",
                Description = "The bank code for the account. For Westpac this is a static value of 03",
                Pattern = @"^\d{2}$"
            },
            ["accountNumber"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Example = "1234567",
                Description = "The account number for the account. This is a seven digit value",
                Pattern = @"^\d{7}$"
            },
            ["branchCode"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Example = "0000",
                Description = "A four digit value assigned based on the branch the account was opened in",
                Pattern = @"^\d{4}$"
            },
            ["accountSuffix"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Example = "000",
                Description =
                    "A three digit value appended to the account number based on the number of accounts opened by the customer",
                Pattern = @"^\d{3}$"
            }
        },
        Example = new JsonObject
        {
            ["bankCode"] = JsonValue.Create("03"),
            ["accountNumber"] = JsonValue.Create("1234567"),
            ["branchCode"] = JsonValue.Create("0000"),
            ["accountSuffix"] = JsonValue.Create("000")
        },
        Required = new HashSet<string>
        {
            "bankCode",
            "accountNumber",
            "branchCode",
            "accountSuffix"
        }
    };
}