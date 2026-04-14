using System.Diagnostics.CodeAnalysis;

namespace Westpac.Evaluation.SavingsAccountCreator.Models;

public readonly record struct ValidatedCustomerName
{
    public required string FirstName { get; init; }

    public required string LastName { get; init; }
}

[method: SetsRequiredMembers]
public readonly record struct ValidatedSavingsAccountRequest(
    ValidatedCustomerName CustomerName,
    string? AccountNickName,
    string IdempotencyKey,
    long CustomerNumber,
    string BranchCode)
{
    public required long CustomerNumber { get; init; } = CustomerNumber;

    public required string IdempotencyKey { get; init; } = IdempotencyKey;

    public required AccountType AccountType { get; init; } = AccountType.Savings;

    public required ValidatedCustomerName CustomerName { get; init; } = CustomerName;

    public required string BranchCode { get; init; } = BranchCode;
}