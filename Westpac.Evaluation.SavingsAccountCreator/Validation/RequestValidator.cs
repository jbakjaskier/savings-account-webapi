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



public class SavingsRequestValidator (IOptions<OffensiveWords> offensiveWordsOptions, ILogger<SavingsRequestValidator> logger, 
    [FromKeyedServices("first-name-validator")] IRequestValidator<string?, string> firstNameLengthValidator, 
    [FromKeyedServices("last-name-validator")] IRequestValidator<string?, string> lastNameLengthValidator, 
    [FromKeyedServices("account-nickname-length-validator")] IRequestValidator<string?, string> accountNickNameLengthValidator) : IRequestValidator<CreateAccountRequest, ValidatedSavingsAccountRequest>
{
    public OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure> Validate(CreateAccountRequest request)
    {
        logger.LogInformation("Validating savings account request with {accountType}, {firstName}, {lastName} and {accountNickName}", request.AccountType, request.CustomerName?.FirstName,request.CustomerName?.LastName ,request.AccountNickName);
        
        if (string.IsNullOrWhiteSpace(request.AccountType) ||
            !Enum.TryParse <AccountType>(request.AccountType, ignoreCase: false,out var parsedAccountType))
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

        if (request.CustomerName is null)
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                new ValidationFailure(SavingsAccountRequestFields.CustomerName, "The customer name is required")
            );
        }

        if (firstNameLengthValidator.Validate(request.CustomerName.FirstName) is OperationResponse<string, ValidationFailure>.FailedOperation
            failedFirstNameValidationResult)
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                failedFirstNameValidationResult.Data
            );
        }
        
        if (lastNameLengthValidator.Validate(request.CustomerName.LastName) is OperationResponse<string, ValidationFailure>.FailedOperation lastNameFailureValidationResult)
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                lastNameFailureValidationResult.Data
            );
        }
        
        if (string.IsNullOrWhiteSpace(request.AccountNickName))
        {
            logger.LogInformation("The savings account request has been successfully validated with {firstName} and {lastName} and {accountNickName}", request.CustomerName.FirstName, request.CustomerName.LastName, null);

            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.SuccessfulOperation(new ValidatedSavingsAccountRequest(
                new ValidatedCustomerName()
                {
                    FirstName = request.CustomerName.FirstName!,
                    LastName = request.CustomerName.LastName!
                },
                AccountNickName: null
                ));

        }
        
        if (accountNickNameLengthValidator.Validate(request.AccountNickName) is OperationResponse<string, ValidationFailure>.FailedOperation accountNickNameFailureValidationResult)
        {
            return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation(
                accountNickNameFailureValidationResult.Data
            );
        }
        
        
        logger.LogInformation("The savings account request has been successfully validated with {firstName} and {lastName} and {accountNickName}", request.CustomerName.FirstName, request.CustomerName.LastName, request.AccountNickName);

        return new OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.SuccessfulOperation(new ValidatedSavingsAccountRequest(
            new ValidatedCustomerName()
            {
                FirstName = request.CustomerName.FirstName!,
                LastName = request.CustomerName.LastName!
            },
            AccountNickName: request.AccountNickName!
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

