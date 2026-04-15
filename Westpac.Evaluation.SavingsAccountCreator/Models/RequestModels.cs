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
}

public record CreateAccountRequest
{
    public string? BranchCode { get; init; }

    public string? AccountType { get; init; }

    public string? CustomerNumber { get; init; }

    public string? AccountNickName { get; init; }
}

public record CustomerName
{
    public string? FirstName { get; init; }

    public string? LastName { get; init; }
}