using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Westpac.Evaluation.DomainModels;
using Westpac.Evaluation.SavingsAccountCreator.Configuration;
using Westpac.Evaluation.SavingsAccountCreator.Models;
using Westpac.Evaluation.SavingsAccountCreator.Persistence;
using Westpac.Evaluation.SavingsAccountCreator.Persistence.Repository;
using Westpac.Evaluation.SavingsAccountCreator.Validation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(opt =>
{
    opt.AddDocumentTransformer((openApiDoc, ctx, ct) =>
    {
        openApiDoc.Info = new OpenApiInfo()
        {
            Title = "Westpac Account Creator API",
            Version = "v1",
            Description = "This API is used to create accounts and customers in Westpac",
            Contact = new OpenApiContact()
            {
                Name = "Service Desk",
                Url = new Uri("https://westpac.com/contact-us"),
                Email = "teamsemail@westpac.com"
            }
        };

        return Task.CompletedTask;
    });
});

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
        return new InputLengthAndNonEmptyStringValidator(logger, 3, 100, RequestFields.FirstName);
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
        return new InputLengthAndNonEmptyStringValidator(logger, 3, 100, RequestFields.LastName);
    });

builder.Services
    .AddKeyedSingleton<IRequestValidator<string?, string>>("account-nickname-length-validator", (sp, key) =>
    {
        //TODO: Can account nick name be whitespaced ?? (We know it CAN be null - but whitespaced is different)
        var logger = sp.GetRequiredService<ILogger<InputLengthAndNonEmptyStringValidator>>();
        return new InputLengthAndNonEmptyStringValidator(logger, 5, 30, RequestFields.AccountNickName);
    });

builder.Services
    .AddKeyedSingleton<IRequestValidator<string?, string>>("idempotency-key-validator", (sp, key) =>
    {
        //TODO: A bit more thought needs to be put into the format of the idempotency key in the first place - Do we want to possibly ONLY restrict it to alphanumeric characters ??
        //Or just have it be a GUID ??
        var logger = sp.GetRequiredService<ILogger<InputLengthAndNonEmptyStringValidator>>();
        return new InputLengthAndNonEmptyStringValidator(logger, 10, 100, RequestFields.IdempotencyKey);
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
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(opt =>
    {
        opt.WithTitle("Westpac Account Creator API");
        opt.WithTheme(ScalarTheme.Default);
        opt.WithDefaultHttpClient(ScalarTarget.Fsharp, ScalarClient.Curl);
    });
}

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
    .WithName("CreateSavingsAccount")
    .AddOpenApiOperationTransformer((opt, ctx, ct) =>
    {
        opt.Summary = "Create a Savings Account for customer";
        opt.Description =
            "You can use this endpoint to create a savings account for a customer. This also enforces an upper limit on the number of accounts a customer can have";

        opt.RequestBody = new OpenApiRequestBody
        {
            Description = "The request body for creating a savings account",
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [MediaTypeNames.Application.Json] = new()
                {
                    Schema = CreateAccountRequest.OpenApiSchema
                }
            }
        };

        opt.Parameters = new List<IOpenApiParameter>
        {
            new OpenApiParameter
            {
                Name = "idempotency-key",
                In = ParameterLocation.Header,
                Required = true,
                Description =
                    "The idempotency key for the request. This is used to ensure that the request is not processed more than once. This is currently NOT implemented",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Example = "123456789",
                    Pattern = @"^[a-zA-Z0-9]+$",
                    MinLength = 10,
                    MaxLength = 100
                }
            }
        };

        opt.Responses = new OpenApiResponses
        {
            ["201"] = new OpenApiResponse
            {
                Description = "The savings account has been successfully created",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [MediaTypeNames.Application.Json] = new()
                    {
                        Schema = AccountResponse.OpenApiSchema
                    }
                }
            },
            ["400"] = new OpenApiResponse
            {
                Description = "The request is invalid",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [MediaTypeNames.Application.Json] = new()
                    {
                        Schema = ValidationFailure.OpenApiSchema
                    }
                }
            },
            ["404"] = new OpenApiResponse
            {
                Description = "The customer does not exist",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [MediaTypeNames.Application.Json] = new()
                    {
                        Schema = ActionFailure.OpenApiSchema
                    }
                }
            },
            ["409"] = new OpenApiResponse
            {
                Description = "The customer has exceeded the maximum number of accounts that can be created",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [MediaTypeNames.Application.Json] = new()
                    {
                        Schema = ActionFailure.OpenApiSchema
                    }
                }
            },
            ["500"] = new OpenApiResponse
            {
                Description = "An unexpected error has occured",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [MediaTypeNames.Application.Json] = new()
                    {
                        Schema = ActionFailure.OpenApiSchema
                    }
                }
            }
        };

        return Task.CompletedTask;
    });


//This can be called multiple times without any side effects
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
    })
    .AddOpenApiOperationTransformer((opt, ctx, ct) =>
    {
        opt.Summary = "Create a customer";
        opt.Description = "This endpoint will create a customer";


        opt.RequestBody = new OpenApiRequestBody
        {
            Description = "The request body for creating a savings account",
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [MediaTypeNames.Application.Json] = new()
                {
                    Schema = CreateCustomerRequest.OpenApiSchema
                }
            }
        };


        opt.Responses = new OpenApiResponses
        {
            ["201"] = new OpenApiResponse
            {
                Description = "The accounts for the customer retrieved",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [MediaTypeNames.Application.Json] = new()
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Array,
                            Items = CustomerResponse.OpenApiSchema
                        }
                    }
                }
            },
            ["400"] = new OpenApiResponse
            {
                Description = "The request is invalid",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [MediaTypeNames.Application.Json] = new()
                    {
                        Schema = ValidationFailure.OpenApiSchema
                    }
                }
            },
            ["500"] = new OpenApiResponse
            {
                Description = "An unexpected error has occured",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [MediaTypeNames.Application.Json] = new()
                    {
                        Schema = ActionFailure.OpenApiSchema
                    }
                }
            }
        };


        return Task.CompletedTask;
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
    }).WithName("GetAccountsForCustomer")
    .AddOpenApiOperationTransformer((opt, ctx, ct) =>
    {
        opt.Summary = "Get accounts for customer";
        opt.Description = "This endpoint will return all the accounts for a customer";

        opt.Parameters = new List<IOpenApiParameter>
        {
            new OpenApiParameter
            {
                Name = "customerNumber",
                In = ParameterLocation.Query,
                Required = true,
                Description =
                    "The customer number of the account. This is the customer number that is shared to the customer and shared across channels including IB, Mobile etc\"",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Example = "123456789",
                    Pattern = @"^\d{1,19}$"
                }
            }
        };


        opt.Responses = new OpenApiResponses
        {
            ["200"] = new OpenApiResponse
            {
                Description = "The accounts for the customer retrieved",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [MediaTypeNames.Application.Json] = new()
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Array,
                            Items = AccountResponse.OpenApiSchema
                        }
                    }
                }
            },
            ["400"] = new OpenApiResponse
            {
                Description = "The request is invalid",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [MediaTypeNames.Application.Json] = new()
                    {
                        Schema = ValidationFailure.OpenApiSchema
                    }
                }
            },
            ["404"] = new OpenApiResponse
            {
                Description = "The customer does not exist",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [MediaTypeNames.Application.Json] = new()
                    {
                        Schema = ActionFailure.OpenApiSchema
                    }
                }
            },

            ["204"] = new OpenApiResponse
            {
                Description = "The customer does not have any accounts"
            },
            ["500"] = new OpenApiResponse
            {
                Description = "An unexpected error has occured",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [MediaTypeNames.Application.Json] = new()
                    {
                        Schema = ActionFailure.OpenApiSchema
                    }
                }
            }
        };


        return Task.CompletedTask;
    });

app.MapHealthChecks("/health/liveness").WithName("Liveness");
app.MapHealthChecks("/health/readiness").WithName("Readiness");


app.Run();