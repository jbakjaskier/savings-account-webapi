using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Reqnroll.Microsoft.Extensions.DependencyInjection;

namespace Westpac.Evaluation.Testings.Integration.SavingsAccountCreator;

[Binding]
public class DependencyInjectionHooks
{
    [ScenarioDependencies] // This is the magic attribute from the Reqnroll.Microsoft.Extensions.DependencyInjection package
    public static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();

        // Register the TestContext as a singleton so the DB stays up for the whole run
        services.AddSingleton<TestContext>();

        // Register your step definitions so they can receive the TestContext
        services.AddScoped<SavingsAccountStepDefinitions>();

        return services;
    }
}