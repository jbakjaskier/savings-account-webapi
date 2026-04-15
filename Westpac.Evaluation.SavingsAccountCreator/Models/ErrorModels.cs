using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.OpenApi;

namespace Westpac.Evaluation.SavingsAccountCreator.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ErrorCode>))]
public enum ErrorCode
{
    ExceededMaxNumberOfAccountsPerCustomer,
    CustomerDoesNotExist,
    CustomerDoesNotHaveAnyAccounts,
    UnknownFailure
}

public readonly record struct ActionFailure(ErrorCode ErrorCode, string ErrorMessage)
{
    public ErrorCode ErrorCode { get; init; } = ErrorCode;

    public string ErrorMessage { get; init; } = ErrorMessage;

    public static OpenApiSchema OpenApiSchema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["errorCode"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "The error code indicating the reason for the failure",
                Example = nameof(ErrorCode.CustomerDoesNotExist),
                Enum = new List<JsonNode>
                {
                    JsonValue.Create(nameof(ErrorCode.ExceededMaxNumberOfAccountsPerCustomer)),
                    JsonValue.Create(nameof(ErrorCode.CustomerDoesNotExist)),
                    JsonValue.Create(nameof(ErrorCode.CustomerDoesNotHaveAnyAccounts)),
                    JsonValue.Create(nameof(ErrorCode.UnknownFailure))
                }
            },
            ["errorMessage"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "A message providing further detail about the failure",
                Example = "The customer does not exist"
            }
        },
        Example = new JsonObject
        {
            ["errorCode"] = JsonValue.Create(nameof(ErrorCode.CustomerDoesNotExist)),
            ["errorMessage"] = JsonValue.Create("The customer does not exist")
        },
        Required = new HashSet<string>
        {
            "errorCode",
            "errorMessage"
        }
    };
}

[method: SetsRequiredMembers]
public readonly record struct ValidationFailure(RequestFields Field, string ErrorMessage)
{
    [JsonPropertyName("field")]
    public required RequestFields Field { get; init; } = Field;

    [JsonPropertyName("errorMessage")]
    public required string ErrorMessage { get; init; } = ErrorMessage;

    public static OpenApiSchema OpenApiSchema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["field"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "The request field that failed validation",
                Example = nameof(RequestFields.AccountType),
                Enum = new List<JsonNode>
                {
                    JsonValue.Create(nameof(RequestFields.AccountType)),
                    JsonValue.Create(nameof(RequestFields.CustomerName)),
                    JsonValue.Create(nameof(RequestFields.FirstName)),
                    JsonValue.Create(nameof(RequestFields.LastName)),
                    JsonValue.Create(nameof(RequestFields.AccountNickName)),
                    JsonValue.Create(nameof(RequestFields.IdempotencyKey)),
                    JsonValue.Create(nameof(RequestFields.CustomerNumber)),
                    JsonValue.Create(nameof(RequestFields.BranchCode))
                }
            },
            ["errorMessage"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "A message describing why the field failed validation",
                Example = "BranchCode must be a 4 digit number"
            }
        },
        Example = new JsonObject
        {
            ["field"] = JsonValue.Create(nameof(RequestFields.BranchCode)),
            ["errorMessage"] = JsonValue.Create("BranchCode must be a 4 digit number")
        },
        Required = new HashSet<string>
        {
            "field",
            "errorMessage"
        }
    };
}

//The string conversion from enum in JSON is so that the request does NOT need to be converted every time back and forth. 
//Also in the future as multiple requests are Added - we may have to update this to an interface.
[JsonConverter(typeof(JsonStringEnumConverter<RequestFields>))]
public enum RequestFields
{
    AccountType,
    CustomerName,
    FirstName,
    LastName,
    AccountNickName,
    IdempotencyKey,
    CustomerNumber,
    BranchCode
}

public static class ActionFailureExtensions
{
    public static IResult GetResult(this ActionFailure actionFailure)
    {
        return actionFailure.ErrorCode switch
        {
            ErrorCode.CustomerDoesNotExist => Results.NotFound(actionFailure),
            ErrorCode.CustomerDoesNotHaveAnyAccounts => Results.NoContent(),
            ErrorCode.ExceededMaxNumberOfAccountsPerCustomer => Results.Conflict(actionFailure),
            ErrorCode.UnknownFailure => Results.Json(actionFailure, statusCode: 500),
            _ => throw new ArgumentOutOfRangeException(nameof(actionFailure.ErrorCode), actionFailure.ErrorCode,
                "Unknown error code")
        };
    }
}