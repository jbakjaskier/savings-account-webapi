using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Westpac.Evaluation.DomainModels;
using Westpac.Evaluation.SavingsAccountCreator.Configuration;
using Westpac.Evaluation.SavingsAccountCreator.Models;
using Westpac.Evaluation.SavingsAccountCreator.Persistence;
using Westpac.Evaluation.SavingsAccountCreator.Persistence.Repository;
using Westpac.Evaluation.SavingsAccountCreator.Validation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

//Map Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("Liveness", () => HealthCheckResult.Healthy())
    .AddCheck("Readiness", () => HealthCheckResult.Healthy());

#region Inject Configuration

builder.Services.Configure<OffensiveWordsConfiguration>(builder.Configuration.GetRequiredSection("OffensiveWords"));
builder.Services.Configure<SavingsAccountCreationConfiguration>(
    builder.Configuration.GetRequiredSection("SavingsAccountCreation"));

#endregion

#region Inject Services

builder.Services
    .AddKeyedSingleton<IRequestValidator<string?, string>>("first-name-validator", (sp, key) =>
    {
        var logger = sp.GetRequiredService<ILogger<InputLengthAndNonEmptyStringValidator>>();


        /**
         *
         * TODO:
         * Some people could and may tend to have LONG names.
         * So if there is a CDD (Customer Due Diligence) process before this API is called - would recommend REMOVING the length constraint compeletely from the names as we CANNOT ACTUALLY prescribe how long a customers name should be
         *
        */
        return new InputLengthAndNonEmptyStringValidator(logger, 3, 100, SavingsAccountRequestFields.FirstName);
    });

builder.Services
    .AddKeyedSingleton<IRequestValidator<string?, string>>("last-name-validator", (sp, key) =>
    {
        var logger = sp.GetRequiredService<ILogger<InputLengthAndNonEmptyStringValidator>>();

        //TODO: We're also intentionally not looking for offensive words in the customers name as it is the customers name. 
        // IF CDD (Customer Due Diligence) has NOT happened before and This is a PUBLIC facing API - in which case we also have to do check the input for offensive words. 
        // However, this is a question for the business - how can we actually say that something in a person's NAME is offensive (cultural context etc - this is not talking about instances wherein it's obviously offensive)

        /**
         *
         * TODO: Another question for the business in this case
         * Are we expecting ONLY english names ?? And can names contain EMOJIs and special characters and foreign characters (example Chinese)??
         * If there are other language names - how are we handling them ?
         */
        return new InputLengthAndNonEmptyStringValidator(logger, 3, 100, SavingsAccountRequestFields.LastName);
    });

builder.Services
    .AddKeyedSingleton<IRequestValidator<string?, string>>("account-nickname-length-validator", (sp, key) =>
    {
        //TODO: Can account nick name be whitespaced ?? (We know it CAN be null - but whitespaced is different)
        var logger = sp.GetRequiredService<ILogger<InputLengthAndNonEmptyStringValidator>>();
        return new InputLengthAndNonEmptyStringValidator(logger, 5, 30, SavingsAccountRequestFields.AccountNickName);
    });

builder.Services
    .AddKeyedSingleton<IRequestValidator<string?, string>>("idempotency-key-validator", (sp, key) =>
    {
        //TODO: A bit more thought needs to be put into the format of the idempotency key in the first place - Do we want to possibly ONLY restrict it to alphanumeric characters ??
        //Or just have it be a GUID ??
        var logger = sp.GetRequiredService<ILogger<InputLengthAndNonEmptyStringValidator>>();
        return new InputLengthAndNonEmptyStringValidator(logger, 10, 100, SavingsAccountRequestFields.IdempotencyKey);
    });

builder.Services
    .AddSingleton<
        IRequestValidator<(CreateAccountRequest requestBody, string? idempotencyKeyFromHeader),
            ValidatedSavingsAccountRequest>, SavingsRequestValidator>();


builder.Services
    .AddSingleton<IRequestValidator<CreateCustomerRequest, ValidatedCreateCustomerRequest>, CreateCustomerValidator>();

builder.Services.AddSingleton<IRequestValidator<string?, long>, CustomerNumberValidator>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AccountDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IAccountRepository, AccountRepository>();

#endregion


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) app.MapOpenApi();

//TODO: We should not do this redirect in the application layer - In Production this would be handled at the Service Mesh (Istio) level
app.UseHttpsRedirection();

var apiGroup = app.MapGroup("api/v1");

//TODO: The idempotency key will need to be implemented in a transactional basis cause there may be downstream systems that could try and call this endpoint mutiple times as part of the retries 
apiGroup.MapPost("/account", ([FromHeader(Name = "idempotency-key")] string? idempotencyKey,
        [FromBody] CreateAccountRequest createAccountRequest,
        IRequestValidator<(CreateAccountRequest requestBody, string? idempotencyKeyFromHeader),
            ValidatedSavingsAccountRequest> savingsAccountValidator,
        IAccountRepository accountRepository, CancellationToken cancellationToken) =>
    {
        return savingsAccountValidator.Validate((createAccountRequest, idempotencyKey)).Match(
            async validatedRequest => await accountRepository.CreateSavingsAccount(validatedRequest, cancellationToken)
                .Match(accountCreated => Results.Json(accountCreated, statusCode: 201),
                    accountCreationFailure => accountCreationFailure.GetResult()
                )
            , validationFailure => Results.BadRequest(validationFailure));
    })
    .WithName("CreateSavingsAccount");


//TODO: The idempotency key will need to be implemented in a transactional basis cause there may be downstream systems that could try and call this endpoint mutiple times as part of the retries - especially given this is a CREATE endpoint
apiGroup.MapPost("/customer", (
    [FromBody] CreateCustomerRequest createCustomerRequest,
    IRequestValidator<CreateCustomerRequest, ValidatedCreateCustomerRequest> createCustomerRequestValidator,
    IAccountRepository accountRepository,
    CancellationToken cancellationToken) =>
{
    return createCustomerRequestValidator.Validate(createCustomerRequest).Match(succeededValidation =>
            accountRepository.CreateCustomer(succeededValidation, cancellationToken).Match(
                createCustomer => Results.Json(createCustomer, statusCode: 201), failure => failure.GetResult()),
        failure => Results.BadRequest(failure));
});

apiGroup.MapGet("/account", ([FromQuery(Name = "customerNumber")] string? customerNumber,
    IRequestValidator<string?, long> customerNumberValidator,
    CancellationToken cancellationToken, IAccountRepository accountRepository) =>
{
    return customerNumberValidator.Validate(customerNumber)
        .Match(
            async validatedCustomerNumber => await accountRepository
                .GetAccountsForCustomer(validatedCustomerNumber, cancellationToken)
                .Match(accountsForCustomer => Results.Json(accountsForCustomer, statusCode: 200),
                    actionFailure => actionFailure.GetResult()), failure => Results.BadRequest(failure));
}).WithName("GetAccountsForCustomer");

app.MapHealthChecks("/health/liveness").WithName("Liveness");
app.MapHealthChecks("/health/readiness").WithName("Readiness");


app.Run();