using Microsoft.Extensions.Options;
using Westpac.Evaluation.DomainModels;
using Westpac.Evaluation.SavingsAccountCreator.Configuration;
using Westpac.Evaluation.SavingsAccountCreator.Models;

namespace Westpac.Evaluation.SavingsAccountCreator.Validation;

public interface IRequestValidator<in TUnValidatedRequest, TValidatedRequest>
{
    /// <summary>
    ///     Validates the incoming request and transforms it into a ValidatedRequest represented by TValidatedRequest
    /// </summary>
    /// <param name="request">
    ///     This is the incoming request model
    /// </param>
    /// <returns>
    ///     A successful response with TValidatedRequest  if the request is valid
    ///     A failure response with the list of validation failures if the request is invalid
    /// </returns>
    //TODO: 
    //Currently Implementing a exit hatch approach wherein if any field is invalid we return a SINGLE ValidationFailure failed response.
    //In the future and based on Requirements we can implement a path where ALL validation failures are returned as one - as in List<ValidationFailure>
    //However, my preference is to have the escape hatch mechanism, as we don't expose our entire APIs validation logic for every field. 
    //The openAPI contract `openapi.json/yaml` already has documented for each item. 
    public OperationResponse<TValidatedRequest, ValidationFailure> Validate(TUnValidatedRequest request);
}

public class SavingsRequestValidator(
    IOptions<OffensiveWordsConfiguration> offensiveWordsOptions,
    IOptions<SavingsAccountCreationConfiguration> savingsAccountCreationOptions,
    ILogger<SavingsRequestValidator> logger,
    [FromKeyedServices("account-nickname-length-validator")]
    IRequestValidator<string?, string> accountNickNameLengthValidator,
    IRequestValidator<string?, long> customerNumberValidator)
    : IRequestValidator<(CreateAccountRequest requestBody, string? idempotencyKeyFromHeader),
        ValidatedSavingsAccountRequest>
{
    public OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure> Validate(
        (CreateAccountRequest requestBody, string? idempotencyKeyFromHeader) request)
    {
        logger.LogInformation(
            "Validating savings account request with {accountType}, {customerNumber} and {accountNickName}",
            request.requestBody.AccountType, request.requestBody.CustomerNumber, request.requestBody.AccountNickName);

        if (string.IsNullOrWhiteSpace(request.requestBody.AccountType) ||
            !Enum.TryParse<AccountType>(request.requestBody.AccountType, false, out var parsedAccountType))
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(RequestFields.AccountType,
                    "The account type is invalid") //Intentionally opaque errors - as we don't want to leak any information about the business logic - however the contract at `openapi.json/yaml` contains the contract in great detail
            );

        //Check if it's a savings account type
        if (parsedAccountType != AccountType.Savings)
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(RequestFields.AccountType, "Only savings account is supported")
            );

        var customerNumberValidationResult = customerNumberValidator.Validate(request.requestBody.CustomerNumber);

        if (customerNumberValidationResult is OperationResponse<long, ValidationFailure>.FailedOperation
            customerNumberFailureValidationResult)
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                customerNumberFailureValidationResult.Data
            );

        var successfulCustomerNumberValidationResult =
            (customerNumberValidationResult as OperationResponse<long, ValidationFailure>.SuccessfulOperation)!;

        if (string.IsNullOrWhiteSpace(request.requestBody.BranchCode))
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(RequestFields.BranchCode, "The branch code is required")
            );

        if (request.requestBody.BranchCode.Length != 4 || !request.requestBody.BranchCode.All(char.IsDigit) ||
            !savingsAccountCreationOptions.Value.ValidBranchCodes.Any(validBranchCode =>
                validBranchCode.Equals(request.requestBody.BranchCode)))
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(RequestFields.BranchCode, "The branch code is invalid")
            );


        if (string.IsNullOrWhiteSpace(request.requestBody.AccountNickName))
        {
            logger.LogInformation(
                "The savings account request has been successfully validated with {customerNumber} and {accountNickName}",
                request.requestBody.CustomerNumber, null);

            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.SuccessfulOperation(
                new ValidatedSavingsAccountRequest(
                    null,
                    successfulCustomerNumberValidationResult.Data,
                    request.requestBody.BranchCode!
                ));
        }

        if (accountNickNameLengthValidator.Validate(request.requestBody.AccountNickName) is
            OperationResponse<string, ValidationFailure>.FailedOperation accountNickNameFailureValidationResult)
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                accountNickNameFailureValidationResult.Data
            );

        //Look into offensive words provided by the config
        //TODO: We should also in the future look at combination of WORDS, SYMBOLS, SPECIAL CHARACTERs and NUMBERs for offensive inputs - For example - B@D
        //TODO: If this is a public facing API would recommend a sentiment analysis integration - however, we're now adding another integration around the same 
        // The current "contains" logic is iffy at best - would be great to have better business requirements around the same. 
        if (offensiveWordsOptions.Value.OffensiveWordsToBeFiltered.Any(offensiveWord =>
                request.requestBody.AccountNickName.Contains(offensiveWord, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning("The {AccountNickname} was recognised as containing offensive words",
                request.requestBody.AccountNickName);

            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(RequestFields.AccountNickName,
                    "The account nickname cannot contain offensive words")
            );
        }


        logger.LogInformation(
            "The savings account request has been successfully validated with {customerNumber} and {accountNickName}",
            request.requestBody.CustomerNumber,
            request.requestBody.AccountNickName);

        return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.SuccessfulOperation(
            new ValidatedSavingsAccountRequest(
                request.requestBody.AccountNickName!,
                successfulCustomerNumberValidationResult.Data,
                request.requestBody.BranchCode!
            ));
    }
}

public class CreateCustomerValidator(
    [FromKeyedServices("first-name-validator")] IRequestValidator<string?, string> firstNameLengthValidator,
    [FromKeyedServices("last-name-validator")]
    IRequestValidator<string?, string> lastNameLengthValidator,
    IRequestValidator<string?, long> customerNumberValidator,
    ILogger<CreateCustomerValidator> logger) : IRequestValidator<CreateCustomerRequest, ValidatedCreateCustomerRequest>
{
    public OperationResponse<ValidatedCreateCustomerRequest, ValidationFailure> Validate(CreateCustomerRequest request)
    {
        logger.LogInformation("Validating {requestModel} with {customerNumber}, {firstName} and {lastName}",
            nameof(CreateCustomerRequest), request.CustomerNumber, request.CustomerName?.FirstName,
            request.CustomerName?.LastName);

        var customerNumberValidationResult = customerNumberValidator.Validate(request.CustomerNumber);

        if (customerNumberValidationResult is OperationResponse<long, ValidationFailure>.FailedOperation
            customerNumberFailureValidationResult)
            return new OperationResponse<ValidatedCreateCustomerRequest, ValidationFailure>.FailedOperation(
                customerNumberFailureValidationResult.Data
            );

        var successfulCustomerNumberValidationResult =
            (customerNumberValidationResult as OperationResponse<long, ValidationFailure>.SuccessfulOperation)!;


        if (request.CustomerName is null)
            return new OperationResponse<ValidatedCreateCustomerRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(RequestFields.CustomerName, "The customer name is required")
            );

        if (firstNameLengthValidator.Validate(request.CustomerName.FirstName) is
            OperationResponse<string, ValidationFailure>.FailedOperation
            failedFirstNameValidationResult)
            return new OperationResponse<ValidatedCreateCustomerRequest, ValidationFailure>.FailedOperation(
                failedFirstNameValidationResult.Data
            );

        if (lastNameLengthValidator.Validate(request.CustomerName.LastName) is
            OperationResponse<string, ValidationFailure>.FailedOperation lastNameFailureValidationResult)
            return new OperationResponse<ValidatedCreateCustomerRequest, ValidationFailure>.FailedOperation(
                lastNameFailureValidationResult.Data
            );


        logger.LogInformation(
            "The customer request has been successfully validated with {customerNumber} and {firstName} and {lastName}",
            request.CustomerNumber, request.CustomerName.FirstName, request.CustomerName.LastName);

        return new OperationResponse<ValidatedCreateCustomerRequest, ValidationFailure>.SuccessfulOperation(
            new ValidatedCreateCustomerRequest(
                successfulCustomerNumberValidationResult.Data,
                new ValidatedCustomerName(
                    request.CustomerName.FirstName!,
                    request.CustomerName.LastName!
                )
            )
        );
    }
}

//TODO: This can be separated into two validators - one for input length and one for empty string in the future.
public class InputLengthAndNonEmptyStringValidator(
    ILogger<InputLengthAndNonEmptyStringValidator> logger,
    int minLengthOfInput,
    int maxLengthOfInput,
    RequestFields field) : IRequestValidator<string?, string>
{
    public OperationResponse<string, ValidationFailure> Validate(string? input)
    {
        logger.LogInformation("Validating input with {input} for {minLength} and {maxLength} of {field}", input,
            minLengthOfInput, maxLengthOfInput, field.ToString());

        if (string.IsNullOrWhiteSpace(input))
            return new OperationResponse<string, ValidationFailure>.FailedOperation(
                new ValidationFailure(field, $"The {field} is required")
            );

        if (input.Length < minLengthOfInput)
            return new OperationResponse<string, ValidationFailure>.FailedOperation(
                new ValidationFailure(field, $"The {field} should be at least {minLengthOfInput} characters long")
            );

        if (input.Length > maxLengthOfInput)
            return new OperationResponse<string, ValidationFailure>.FailedOperation(
                new ValidationFailure(field, $"The {field} cannot be more than {maxLengthOfInput} characters long")
            );

        logger.LogInformation(
            "String length validation was successful for {input} with {minLength} and {maxLength} for {field}", input,
            minLengthOfInput, maxLengthOfInput, field.ToString());

        return new OperationResponse<string, ValidationFailure>.SuccessfulOperation(input);
    }
}

//TODO: The customer number is a unique identifier for a customer. and we don't have any min length validation for it.
public class CustomerNumberValidator(ILogger<CustomerNumberValidator> logger) : IRequestValidator<string?, long>
{
    public OperationResponse<long, ValidationFailure> Validate(string? input)
    {
        logger.LogInformation("Validating customer number with {input}", input);

        if (string.IsNullOrWhiteSpace(input))
            return new OperationResponse<long, ValidationFailure>.FailedOperation(
                new ValidationFailure(RequestFields.CustomerNumber, "The customer number is required")
            );

        if (!input.All(char.IsDigit) || !long.TryParse(input, out var customerNumberParsed))
            return new OperationResponse<long, ValidationFailure>.FailedOperation(
                new ValidationFailure(RequestFields.CustomerNumber, "The customer number is invalid")
            );

        logger.LogInformation("Customer number validation was successful for {input}", input);

        return new OperationResponse<long, ValidationFailure>.SuccessfulOperation(customerNumberParsed);
    }
}