using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Westpac.Evaluation.SavingsAccountCreator.Persistence;

public class AccountDbContextDesignFactory : IDesignTimeDbContextFactory<AccountDbContext>
{
    public AccountDbContext CreateDbContext(string[] args)
    {
        //TODO: Ideally it would be better if we're using Token Based Authentication that RDS (or other providers) offers and not a connection string.
        string? connectionString = Environment.GetEnvironmentVariable("MIGRATION_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception("The connection string is not set for migrations");
        }
        
        var optionsBuilder = new DbContextOptionsBuilder<AccountDbContext>()
            .UseNpgsql(connectionString, x => x.MigrationsAssembly($"{typeof(AccountDbContextDesignFactory).Assembly.GetName().Name}"));
        
        return new AccountDbContext(optionsBuilder.Options);
        
        
    }
}