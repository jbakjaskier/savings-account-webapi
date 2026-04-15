using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Westpac.Evaluation.SavingsAccountCreator.Persistence;

namespace Westpac.Evaluation.Testings.Integration.SavingsAccountCreator;

    public class TestContext : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("test_db")
            .WithUsername("admin")
            .WithPassword("password")
            .Build();

        public HttpClient Client { get; private set; } = default!;

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
        }
        

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
    }
    