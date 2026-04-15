using Microsoft.EntityFrameworkCore;
using Westpac.Evaluation.SavingsAccountCreator.Persistence.Entity;

namespace Westpac.Evaluation.SavingsAccountCreator.Persistence;

public static class AccountDbContextConstants
{
    public const string AccountSchemaName = "BankAccounts";

    public const string AccountNumberSequenceName = "AccountNumberSequence";

    public const string AccountNumberSequenceGeneratorStoredProcedureName = "create_account_sequence";

    public const string GetNextAccountNumberStoredProcedureName = "get_next_account_number";
}

public class AccountDbContext(DbContextOptions<AccountDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts { get; set; }

    public DbSet<Customer> Customers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //TODO: We may want to change this to a different schema in the future - Currently, we are using the same schema for all tables
        //Also Snake casing is applied at the db context level - we may want to discuss and update it in the future. 
        modelBuilder.HasDefaultSchema(AccountDbContextConstants.AccountSchemaName);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new CustomerEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new AccountEntityTypeConfiguration());
    }
}