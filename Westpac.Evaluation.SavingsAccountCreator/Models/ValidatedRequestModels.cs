using System.Diagnostics.CodeAnalysis;

namespace Westpac.Evaluation.SavingsAccountCreator.Models;

[method: SetsRequiredMembers]
public readonly record struct ValidatedCreateCustomerRequest(long CustomerNumber, ValidatedCustomerName CustomerName)
{
    public required long CustomerNumber { get; init; } = CustomerNumber;

    public required ValidatedCustomerName CustomerName { get; init; } = CustomerName;
}

[method: SetsRequiredMembers]
public readonly record struct ValidatedCustomerName(string FirstName, string LastName)
{
    public required string FirstName { get; init; } = FirstName;

    public required string LastName { get; init; } = LastName;
}

[method: SetsRequiredMembers]
public readonly record struct ValidatedSavingsAccountRequest(
    string? AccountNickName,
    long CustomerNumber,
    string BranchCode)
{
    public required long CustomerNumber { get; init; } = CustomerNumber;

    public required AccountType AccountType { get; init; } = AccountType.Savings;

    public required string BranchCode { get; init; } = BranchCode;
}