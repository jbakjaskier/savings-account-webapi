using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Westpac.Evaluation.SavingsAccountCreator.Persistence;
using Westpac.Evaluation.SavingsAccountCreator.Persistence.Entity;

namespace Westpac.Evaluation.Testings.Integration.SavingsAccountCreator;

public class TestContext : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .WithDatabase("test_db")
        .WithUsername("admin")
        .WithPassword("password")
        .Build();

    public HttpClient Client { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        // Apply Migrations
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        await db.Database.MigrateAsync();

        Client = CreateClient();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
        await _dbContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _dbContainer.GetConnectionString(),
                ["OffensiveWords:OffensiveWordsToBeFiltered"] = "[\"badword1\", \"badword2\"]",
                ["SavingsAccountCreation:MaxAccountsPerCustomer"] = "5",
                ["SavingsAccountCreation:ValidBranchCodes"] = "[\"0001\",\"0004\",\"1243\"]"
            });
        });


        // 2. Forcefully override the DbContext registration
        builder.ConfigureTestServices(services =>
        {
            // Remove the existing DbContextOptions that were locked in with the wrong connection string
            services.RemoveAll(typeof(DbContextOptions<AccountDbContext>));
            services.RemoveAll(typeof(AccountDbContext));

            // Re-register the DbContext to use the Testcontainers dynamic connection string
            services.AddDbContext<AccountDbContext>(options =>
            {
                options.UseNpgsql(_dbContainer.GetConnectionString());
            });
        });
    }

    public async Task StartPostgres()
    {
        await _dbContainer.StartAsync();
    }

    public async Task TemporarilyDisablePostgres()
    {
        await _dbContainer.StopAsync();
    }

    public async Task<List<Account>> GetAccountsForCustomer(long customerNumber)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountDbContext>();

        return await db.Customers
            .AsNoTracking()
            .Include(x => x.Accounts)
            .Where(x => x.CustomerNumber == customerNumber && x.Accounts != null)
            .SelectMany(x => x.Accounts!)
            .ToListAsync();
    }

    public async Task CreateMultipleAccountsForCustomer(long customerNumber, int numberOfAccounts)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var random = new Random();

        var customerInDb = await db.Customers
            .AsNoTracking()
            .SingleAsync(x => x.CustomerNumber == customerNumber);


        for (var i = 0; i < numberOfAccounts; i++)
            await db.Accounts.AddAsync(new Account
            {
                Id = Guid.CreateVersion7(),
                BranchCode = random.Next(1000, 10000).ToString("D4"),
                AccountNumber = i.ToString("D7"),
                AccountSuffix = i.ToString("D3"),
                CustomerId = customerInDb.CustomerId,
                CreatedAt = DateTimeOffset.UtcNow
            });

        await db.SaveChangesAsync();
    }

    public async Task<Guid> CreateCustomer(long customerNumber, string firstName, string lastName)
    {
        var newAccountId = Guid.CreateVersion7();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        await db.Customers.AddAsync(new Customer
        {
            CustomerNumber = customerNumber,
            FirstName = firstName,
            LastName = lastName,
            CustomerId = newAccountId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        return newAccountId;
    }
}