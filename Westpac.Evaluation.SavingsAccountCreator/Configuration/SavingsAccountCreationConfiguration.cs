namespace Westpac.Evaluation.SavingsAccountCreator.Configuration;

public record SavingsAccountCreationConfiguration
{
    public required int MaxAccountsPerCustomer { get; init; }

    public required string[] ValidBranchCodes { get; init; }
}