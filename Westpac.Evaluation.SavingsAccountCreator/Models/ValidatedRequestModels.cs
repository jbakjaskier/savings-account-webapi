using System.Diagnostics.CodeAnalysis;

namespace Westpac.Evaluation.SavingsAccountCreator.Models;


public readonly record struct ValidatedCustomerName
{
    public required string FirstName { get; init; }
    
    public required string LastName { get; init; }
}


[method:SetsRequiredMembers]
public readonly record struct ValidatedSavingsAccountRequest(
    ValidatedCustomerName CustomerName,
    string? AccountNickName)
{
    public required AccountType AccountType { get; init; } = AccountType.Savings;
    
    public required ValidatedCustomerName CustomerName { get; init; } = CustomerName;
}