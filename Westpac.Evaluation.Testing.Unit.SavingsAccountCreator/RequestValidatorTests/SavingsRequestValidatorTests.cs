using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Westpac.Evaluation.DomainModels;
using Westpac.Evaluation.SavingsAccountCreator.Configuration;
using Westpac.Evaluation.SavingsAccountCreator.Models;
using Westpac.Evaluation.SavingsAccountCreator.Validation;

namespace Westpac.Evaluation.Testing.Unit.SavingsAccountCreator.RequestValidatorTests;

public class SavingsRequestValidatorTests
{
    private readonly Mock<IRequestValidator<string?, long>> _customerNumMock;
    private readonly Mock<ILogger<SavingsRequestValidator>> _loggerMock;
    private readonly Mock<IRequestValidator<string?, string>> _nickNameMock;
    private readonly OffensiveWordsConfiguration _offensiveConfig;

    private readonly SavingsAccountCreationConfiguration _savingsConfig;
    private readonly SavingsRequestValidator _sut;

    public SavingsRequestValidatorTests()
    {
        _nickNameMock = new Mock<IRequestValidator<string?, string>>();
        _customerNumMock = new Mock<IRequestValidator<string?, long>>();
        _loggerMock = new Mock<ILogger<SavingsRequestValidator>>();

        _savingsConfig = new SavingsAccountCreationConfiguration
        {
            ValidBranchCodes = ["1234", "5678"],
            MaxAccountsPerCustomer = 5
        };
        _offensiveConfig = new OffensiveWordsConfiguration
        {
            OffensiveWordsToBeFiltered = ["BadWord"]
        };

        _sut = new SavingsRequestValidator(
            Options.Create(_offensiveConfig),
            Options.Create(_savingsConfig),
            _loggerMock.Object,
            _nickNameMock.Object,
            _customerNumMock.Object
        );
    }

    [Fact]
    public void Validate_WhenAllInputsValid_ReturnsSuccess()
    {
        // Arrange
        var requestBody = new CreateAccountRequest
        {
            AccountType = "Savings",
            CustomerNumber = "999",
            BranchCode = "1234",
            AccountNickName = "My Savings"
        };
        const string headerKey = "unique-key";

        SetupCustomerSuccess(999L);
        SetupNickNameSuccess("My Savings");

        // Act
        var result = _sut.Validate((requestBody, headerKey));

        // Assert
        var success =
            Assert.IsType<OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.SuccessfulOperation>(
                result);
        Assert.Equal(999L, success.Data.CustomerNumber);
        Assert.Equal("1234", success.Data.BranchCode);
        Assert.Equal("My Savings", success.Data.AccountNickName);
    }

    [Fact]
    public void Validate_InvalidAccountType_ReturnsFailure()
    {
        // Arrange
        var request = new CreateAccountRequest { AccountType = "Checking" };
        

        // Act
        var result = _sut.Validate((request, "key"));

        // Assert
        var failure =
            Assert.IsType<OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation>(result);
        Assert.Equal(RequestFields.AccountType, failure.Data.Field);
    }

    [Theory]
    [InlineData("123")] // Too short
    [InlineData("12345")] // Too long
    [InlineData("abcd")] // Non-numeric
    [InlineData("9999")] // Numeric but not in valid list
    public void Validate_BranchCodeRules_ReturnFailure(string branchCode)
    {
        // Arrange
        var request = new CreateAccountRequest { AccountType = "Savings", BranchCode = branchCode };
        
        SetupCustomerSuccess(1L);

        // Act
        var result = _sut.Validate((request, "key"));

        // Assert
        var failure =
            Assert.IsType<OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation>(result);
        Assert.Equal(RequestFields.BranchCode, failure.Data.Field);
    }

    [Fact]
    public void Validate_NullNickName_IsAllowedAndReturnsSuccess()
    {
        // Arrange
        var request = new CreateAccountRequest
        {
            AccountType = "Savings",
            CustomerNumber = "1",
            BranchCode = "1234",
            AccountNickName = null
        };
        
        SetupCustomerSuccess(1L);

        // Act
        var result = _sut.Validate((request, "key"));

        // Assert
        Assert.IsType<OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.SuccessfulOperation>(result);
        _nickNameMock.Verify(x => x.Validate(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void Validate_OffensiveNickName_ReturnsFailureAndLogsWarning()
    {
        // Arrange
        var request = new CreateAccountRequest
        {
            AccountType = "Savings",
            BranchCode = "1234",
            AccountNickName = "Contains BadWord here"
        };
        
        SetupCustomerSuccess(1L);
        SetupNickNameSuccess("Contains BadWord here");

        // Act
        var result = _sut.Validate((request, "key"));

        // Assert
        var failure =
            Assert.IsType<OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation>(result);
        Assert.Equal(RequestFields.AccountNickName, failure.Data.Field);
        Assert.Contains("offensive", failure.Data.ErrorMessage);

        _loggerMock.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("offensive")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("NotAnEnum")]
    [InlineData("99")] // Enum.TryParse might succeed if numeric, but only if defined
    public void Validate_WhenAccountTypeIsInvalid_ReturnsOpaqueFailure(string? accountType)
    {
        // Arrange
        
        var request = new CreateAccountRequest { AccountType = accountType };

        // Act
        var result = _sut.Validate((request, "key"));

        // Assert
        var failure =
            Assert.IsType<OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation>(result);
        Assert.Equal(RequestFields.AccountType, failure.Data.Field);
        
        var expectedPhrases = new List<string> { "The account type is invalid", "Only savings account is supported" };
        Assert.Contains(failure.Data.ErrorMessage, expectedPhrases);
    }

    [Fact]
    public void Validate_WhenCustomerNumberValidatorFails_ReturnsFailure()
    {
        // Arrange
        
        
        var request = new CreateAccountRequest { AccountType = "Savings", CustomerNumber = "abc" };
        var failure = new ValidationFailure(RequestFields.CustomerNumber, "Must be numeric");

        _customerNumMock
            .Setup(x => x.Validate("abc"))
            .Returns(new OperationResponse<long, ValidationFailure>.FailedOperation(failure));

        // Act
        var result = _sut.Validate((request, "key"));

        // Assert
        var resultFailure =
            Assert.IsType<OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation>(result);
        Assert.Equal(failure, resultFailure.Data);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenBranchCodeIsMissing_ReturnsFailure(string? branchCode)
    {
        // Arrange
        
        SetupCustomerSuccess(1L);
        var request = new CreateAccountRequest { AccountType = "Savings", BranchCode = branchCode };

        // Act
        var result = _sut.Validate((request, "key"));

        // Assert
        var failure =
            Assert.IsType<OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation>(result);
        Assert.Equal(RequestFields.BranchCode, failure.Data.Field);
        Assert.Equal("The branch code is required", failure.Data.ErrorMessage);
    }

    [Fact]
    public void Validate_WhenAccountNickNameValidatorFails_ReturnsFailure()
    {
        // Arrange
        
        SetupCustomerSuccess(1L);
        var request = new CreateAccountRequest
        {
            AccountType = "Savings",
            BranchCode = "1234",
            AccountNickName = "TooLong"
        };
        var failure = new ValidationFailure(RequestFields.AccountNickName, "Max 10 chars");

        _nickNameMock
            .Setup(x => x.Validate("TooLong"))
            .Returns(new OperationResponse<string, ValidationFailure>.FailedOperation(failure));

        // Act
        var result = _sut.Validate((request, "key"));

        // Assert
        var resultFailure =
            Assert.IsType<OperationResponse<ValidatedSavingsAccountRequest, ValidationFailure>.FailedOperation>(result);
        Assert.Equal(failure, resultFailure.Data);
    }

    #region Helpers
    private void SetupCustomerSuccess(long val)
    {
        _customerNumMock.Setup(x => x.Validate(It.IsAny<string?>()))
            .Returns(new OperationResponse<long, ValidationFailure>.SuccessfulOperation(val));
    }

    private void SetupNickNameSuccess(string val)
    {
        _nickNameMock.Setup(x => x.Validate(val))
            .Returns(new OperationResponse<string, ValidationFailure>.SuccessfulOperation(val));
    }

    #endregion
}