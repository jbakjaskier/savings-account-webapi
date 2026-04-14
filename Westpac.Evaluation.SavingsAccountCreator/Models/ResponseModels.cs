namespace Westpac.Evaluation.SavingsAccountCreator.Models;

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
}