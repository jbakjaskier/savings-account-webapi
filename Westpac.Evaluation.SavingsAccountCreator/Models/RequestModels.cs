using System.Text.Json.Serialization;

namespace Westpac.Evaluation.SavingsAccountCreator.Models;

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
    CustomerNumber
}



public enum AccountType
{
    Savings,
    Checking
}


public record CreateAccountRequest
{
    public string? AccountType { get; init; }
    
    public string? CustomerNumber { get; init; }
    
    public CustomerName? CustomerName { get; init; }
    
    public string? AccountNickName { get; init; }
}


public record CustomerName
{
    public string? FirstName { get; init; }
    
    public string? LastName { get; init; }
}
