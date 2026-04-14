using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Westpac.Evaluation.DomainModels;
using Westpac.Evaluation.SavingsAccountCreator.Models;
using Westpac.Evaluation.SavingsAccountCreator.Validation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

//Map Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("Liveness", () => HealthCheckResult.Healthy())
    .AddCheck("Readiness", () => HealthCheckResult.Healthy());

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
        var logger = sp.GetRequiredService<ILogger<InputLengthAndNonEmptyStringValidator>>();
        return new InputLengthAndNonEmptyStringValidator(logger, 5, 30, SavingsAccountRequestFields.AccountNickName);
    });

builder.Services
    .AddSingleton<IRequestValidator<CreateAccountRequest, ValidatedSavingsAccountRequest>, SavingsRequestValidator>();


#endregion


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


var apiGroup = app.MapGroup("api/v1");

apiGroup.MapPost("/account", ([FromBody] CreateAccountRequest createAccountRequest, IRequestValidator<CreateAccountRequest, ValidatedSavingsAccountRequest> validator) =>
    {
        var validationResult = validator.Validate(createAccountRequest);

        if (validationResult is OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation
            failedOperation)
        {
            return Results.BadRequest(failedOperation.Data);
        }

        return Results.Created();

    })
    .WithName("GetWeatherForecast");

app.MapHealthChecks("/health/liveness").WithName("Liveness");
app.MapHealthChecks("/health/readiness").WithName("Readiness");


app.Run();

