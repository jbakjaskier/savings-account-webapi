using Microsoft.Extensions.Options;
using Westpac.Evaluation.DomainModels;
using Westpac.Evaluation.SavingsAccountCreator.Configuration;
using Westpac.Evaluation.SavingsAccountCreator.Models;

namespace Westpac.Evaluation.SavingsAccountCreator.Validation;


public interface IRequestValidator<in TUnValidatedRequest, TValidatedRequest>
{
    /// <summary>
    /// Validates the incoming request and transforms it into a ValidatedRequest represented by TValidatedRequest
    /// </summary>
    /// <param name="request">
    /// This is the incoming request model
    /// </param>
    /// <returns>
    ///  A successful response with TValidatedRequest  if the request is valid
    ///  A failure response with the list of validation failures if the request is invalid
    /// </returns>
    ///
    /// 
    //TODO: 
    //Currently Implementing a exit hatch approach wherein if any field is invalid we return a SINGLE ValidationFailure failed response.
    //In the future and based on Requirements we can implement a path where ALL validation failures are returned as one - as in List<ValidationFailure>
    //However, my preference is to have the escape hatch mechanism, as we don't expose our entire APIs validation logic for every field. 
    //The openAPI contract `openapi.json/yaml` already has documented for each item. 

    
    public OperationResponse<TValidatedRequest, ValidationFailure> Validate(TUnValidatedRequest request);
}



public class SavingsRequestValidator (IOptions<OffensiveWordsConfiguration> offensiveWordsOptions, 
    IOptions<SavingsAccountCreationConfiguration> savingsAccountCreationOptions,
    ILogger<SavingsRequestValidator> logger, 
    [FromKeyedServices("first-name-validator")] IRequestValidator<string?, string> firstNameLengthValidator, 
    [FromKeyedServices("last-name-validator")] IRequestValidator<string?, string> lastNameLengthValidator, 
    [FromKeyedServices("idempotency-key-validator")] IRequestValidator<string?, string> idempotencyKeyValidator,
    [FromKeyedServices("account-nickname-length-validator")] IRequestValidator<string?, string> accountNickNameLengthValidator) : IRequestValidator<(CreateAccountRequest requestBody, string? idempotencyKeyFromHeader), ValidatedSavingsAccountRequest>
{
    public OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure> Validate((CreateAccountRequest requestBody, string? idempotencyKeyFromHeader) request)
    {

        if (string.IsNullOrWhiteSpace(request.idempotencyKeyFromHeader))
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(SavingsAccountRequestFields.IdempotencyKey, "The idempotency key is required in the header")
            );
        }

        if (idempotencyKeyValidator.Validate(request.idempotencyKeyFromHeader) is
            OperationResponse<string, ValidationFailure>.FailedOperation idempotencyKeyFailureValidationResult)
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                idempotencyKeyFailureValidationResult.Data
            );
        }
        
        logger.LogInformation("Validating savings account request with {accountType}, {firstName}, {lastName} and {accountNickName}", request.requestBody.AccountType, request.requestBody.CustomerName?.FirstName,request.requestBody.CustomerName?.LastName ,request.requestBody.AccountNickName);
        
        if (string.IsNullOrWhiteSpace(request.requestBody.AccountType) ||
            !Enum.TryParse <AccountType>(request.requestBody.AccountType, ignoreCase: false,out var parsedAccountType))
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(SavingsAccountRequestFields.AccountType, "The account type is invalid") //Intentionally opaque errors - as we don't want to leak any information about the business logic - however the contract at `openapi.json/yaml` contains the contract in great detail
            );
        }
        
        //Check if it's a savings account type
        if (parsedAccountType != AccountType.Savings)
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(SavingsAccountRequestFields.AccountType, "Only savings account is supported")
            );
        }

        if (string.IsNullOrWhiteSpace(request.requestBody.CustomerNumber))
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(SavingsAccountRequestFields.CustomerNumber, "The customer number is required")
            );
        }

        if (!request.requestBody.CustomerNumber.All(char.IsDigit)|| !long.TryParse(request.requestBody.CustomerNumber, out var customerNumberParsed))
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(SavingsAccountRequestFields.CustomerNumber, "The customer number is invalid")
            );
        }

        if (string.IsNullOrWhiteSpace(request.requestBody.BranchCode))
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(SavingsAccountRequestFields.BranchCode, "The branch code is required")
            );
        }

        if (request.requestBody.BranchCode.Length != 4 || !request.requestBody.BranchCode.All(char.IsDigit) || !savingsAccountCreationOptions.Value.ValidBranchCodes.Any(validBranchCode =>
                validBranchCode.Equals(request.requestBody.BranchCode)))
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(SavingsAccountRequestFields.BranchCode, "The branch code is invalid")
            );
        }

        if (request.requestBody.CustomerName is null)
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(SavingsAccountRequestFields.CustomerName, "The customer name is required")
            );
        }

        if (firstNameLengthValidator.Validate(request.requestBody.CustomerName.FirstName) is OperationResponse<string, ValidationFailure>.FailedOperation
            failedFirstNameValidationResult)
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                failedFirstNameValidationResult.Data
            );
        }
        
        if (lastNameLengthValidator.Validate(request.requestBody.CustomerName.LastName) is OperationResponse<string, ValidationFailure>.FailedOperation lastNameFailureValidationResult)
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                lastNameFailureValidationResult.Data
            );
        }
        
        if (string.IsNullOrWhiteSpace(request.requestBody.AccountNickName))
        {
            logger.LogInformation("The savings account request has been successfully validated with {firstName} and {lastName} and {accountNickName}", request.requestBody.CustomerName.FirstName, request.requestBody.CustomerName.LastName, null);

            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.SuccessfulOperation(new ValidatedSavingsAccountRequest(
                new ValidatedCustomerName()
                {
                    FirstName = request.requestBody.CustomerName.FirstName!,
                    LastName = request.requestBody.CustomerName.LastName!
                },
                AccountNickName: null,
                IdempotencyKey: request.idempotencyKeyFromHeader!,
                CustomerNumber: customerNumberParsed,
                BranchCode: request.requestBody.BranchCode!
                ));

        }
        
        if (accountNickNameLengthValidator.Validate(request.requestBody.AccountNickName) is OperationResponse<string, ValidationFailure>.FailedOperation accountNickNameFailureValidationResult)
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                accountNickNameFailureValidationResult.Data
            );
        }
        
        //Look into offensive words provided by the config
        //TODO: We should also in the future look at combination of WORDS, SYMBOLS, SPECIAL CHARACTERs and NUMBERs for offensive inputs - For example - B@D
        //TODO: If this is a public facing API would recommend a sentiment analysis integration - however, we're now adding another integration around the same 
        // The current "contains" logic is iffy at best - would be great to have better business requirements around the same. 
        if (offensiveWordsOptions.Value.OffensiveWordsToBeFiltered.Any(offensiveWord =>
                request.requestBody.AccountNickName.Contains(offensiveWord, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning("The {AccountNickname} was recognised as containing offensive words", request.requestBody.AccountNickName);
            
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(SavingsAccountRequestFields.AccountNickName, "The account nickname cannot contain offensive words")
            );
        }
        
        
        logger.LogInformation("The savings account request has been successfully validated with {firstName} and {lastName} and {accountNickName}", request.requestBody.CustomerName.FirstName, request.requestBody.CustomerName.LastName, request.requestBody.AccountNickName);

        return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.SuccessfulOperation(new ValidatedSavingsAccountRequest(
            new ValidatedCustomerName()
            {
                FirstName = request.requestBody.CustomerName.FirstName!,
                LastName = request.requestBody.CustomerName.LastName!
            },
            AccountNickName: request.requestBody.AccountNickName!,
            IdempotencyKey: request.idempotencyKeyFromHeader!,
            CustomerNumber: customerNumberParsed,
            BranchCode: request.requestBody.BranchCode!
        ));
        
    }
    
}


//TODO: This can be separated into two validators - one for input length and one for empty string in the future.
public class InputLengthAndNonEmptyStringValidator(ILogger<InputLengthAndNonEmptyStringValidator> logger, int minLengthOfInput, int maxLengthOfInput, SavingsAccountRequestFields field) : IRequestValidator<string?, string>
{
    
    public OperationResponse<string, ValidationFailure> Validate(string? input)
    {
        
        logger.LogInformation("Validating input with {input} for {minLength} and {maxLength} of {field}", input, minLengthOfInput, maxLengthOfInput, field.ToString());
        
        if (string.IsNullOrWhiteSpace(input))
        {
            return new OperationResponse<string, ValidationFailure>.FailedOperation(
                new ValidationFailure(field, $"The {field} is required")
            );
        }
        
        if (input.Length < minLengthOfInput)
        {
            return new OperationResponse<string, ValidationFailure>.FailedOperation(
                new ValidationFailure(field, $"The {field} should be at least {minLengthOfInput} characters long")
            );
        }

        if (input.Length > maxLengthOfInput)
        {
            return new OperationResponse<string, ValidationFailure>.FailedOperation(
                new ValidationFailure(field, $"The {field} cannot be more than {maxLengthOfInput} characters long")
            );
        }
        
        logger.LogInformation(
            "String length validation was successful for {input} with {minLength} and {maxLength} for {field}", input,
            minLengthOfInput, maxLengthOfInput, field.ToString());
        
        return new OperationResponse<string, ValidationFailure>.SuccessfulOperation(input);
        
    }
}

