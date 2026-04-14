using System.Text.Json.Serialization;

namespace Westpac.Evaluation.SavingsAccountCreator.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ErrorCode>))]
public enum ErrorCode
{
    ExceededMaxNumberOfAccounts,
    CustomerDoesNotExist,
    CustomerDoesNotHaveAnyAccounts,
    UnknownFailure
}

public readonly record struct ActionFailure(ErrorCode ErrorCode, string ErrorMessage);

public readonly record struct ValidationFailure(SavingsAccountRequestFields Field, string ErrorMessage);

//The string conversion from enum in JSON is so that the request does NOT need to be converted every time back and forth. 
//Also in the future as multiple requests are Added - we may have to update this to an interface.
[JsonConverter(typeof(JsonStringEnumConverter<SavingsAccountRequestFields>))]
public enum SavingsAccountRequestFields
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
            ErrorCode.ExceededMaxNumberOfAccounts => Results.Conflict(actionFailure),
            ErrorCode.UnknownFailure => Results.BadRequest(actionFailure),
            _ => throw new ArgumentOutOfRangeException(nameof(actionFailure.ErrorCode), actionFailure.ErrorCode,
                "Unknown error code")
        };
    }
}