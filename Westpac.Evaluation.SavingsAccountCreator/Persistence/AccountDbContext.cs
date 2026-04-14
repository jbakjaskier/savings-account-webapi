using Microsoft.EntityFrameworkCore;
using Westpac.Evaluation.SavingsAccountCreator.Persistence.Entity;

namespace Westpac.Evaluation.SavingsAccountCreator.Persistence;

public static class AccountDbContextConstants
{
    
    public const string SchemaName = "BankAccounts";
    
    public const string AccountNumberSequenceName = "AccountNumberSequence";

}

public class AccountDbContext(DbContextOptions<AccountDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts { get; set; }

    public DbSet<Customer> Customers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //TODO: We may want to change this to a different schema in the future - Currently, we are using the same schema for all tables
        //Also Snake casing is applied at the db context level - we may want to discuss and update it in the future. 
        modelBuilder.HasDefaultSchema(AccountDbContextConstants.SchemaName);

        base.OnModelCreating(modelBuilder);
        
        modelBuilder.HasSequence<int>(AccountDbContextConstants.AccountNumberSequenceName)
            .StartsAt(0)
            .IncrementsBy(1)
            .HasMin(0)
            .HasMax(9999999)
            .IsCyclic(false); // Prevents it from restarting at 1 if it hits the max


        modelBuilder.ApplyConfiguration(new CustomerEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new AccountEntityTypeConfiguration());
    }
}