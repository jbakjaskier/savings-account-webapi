namespace Westpac.Evaluation.SavingsAccountCreator.Configuration;

public record OffensiveWords
{
    public required string[] OffensiveWordsToBeFiltered { get; init; }
}