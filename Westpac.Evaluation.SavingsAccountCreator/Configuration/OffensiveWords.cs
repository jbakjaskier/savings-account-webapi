namespace Westpac.Evaluation.SavingsAccountCreator.Configuration;

public record OffensiveWordsConfiguration
{
    public required string[] OffensiveWordsToBeFiltered { get; init; }
}