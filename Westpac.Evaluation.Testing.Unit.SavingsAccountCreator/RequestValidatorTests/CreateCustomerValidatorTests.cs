using Microsoft.Extensions.Logging;
using Moq;
using Westpac.Evaluation.DomainModels;
using Westpac.Evaluation.SavingsAccountCreator.Models;
using Westpac.Evaluation.SavingsAccountCreator.Validation;

namespace Westpac.Evaluation.Testing.Unit.SavingsAccountCreator.RequestValidatorTests;

public class CreateCustomerValidatorTests
{
    private readonly Mock<IRequestValidator<string?, long>> _customerNumberValidatorMock;
    private readonly Mock<IRequestValidator<string?, string>> _firstNameValidatorMock;
    private readonly Mock<IRequestValidator<string?, string>> _lastNameValidatorMock;
    private readonly Mock<ILogger<CreateCustomerValidator>> _loggerMock;
    private readonly CreateCustomerValidator _sut;

    public CreateCustomerValidatorTests()
    {
        _firstNameValidatorMock = new Mock<IRequestValidator<string?, string>>();
        _lastNameValidatorMock = new Mock<IRequestValidator<string?, string>>();
        _customerNumberValidatorMock = new Mock<IRequestValidator<string?, long>>();
        _loggerMock = new Mock<ILogger<CreateCustomerValidator>>();

        _sut = new CreateCustomerValidator(
            _firstNameValidatorMock.Object,
            _lastNameValidatorMock.Object,
            _customerNumberValidatorMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public void Validate_WhenAllFieldsAreValid_ReturnsSuccessfulOperation()
    {
        // Arrange
        var request = new CreateCustomerRequest
        {
            CustomerNumber = "12345",
            CustomerName = new CustomerName { FirstName = "John", LastName = "Doe" }
        };

        SetupCustomerNumberSuccess(12345L);
        SetupFirstNameSuccess("John");
        SetupLastNameSuccess("Doe");

        // Act
        var result = _sut.Validate(request);

        // Assert
        var success =
            Assert.IsType<OperationResponse<ValidatedCreateCustomerRequest, ValidationFailure>.SuccessfulOperation>(
                result);
        Assert.Equal(12345L, success.Data.CustomerNumber);
        Assert.Equal("John", success.Data.CustomerName.FirstName);
        Assert.Equal("Doe", success.Data.CustomerName.LastName);

        VerifyLogContains("successfully validated");
    }

    [Fact]
    public void Validate_WhenCustomerNumberFails_ReturnsFailureImmediately()
    {
        // Arrange
        var request = new CreateCustomerRequest { CustomerNumber = "invalid" };
        var failure = new ValidationFailure(RequestFields.CustomerNumber, "Invalid Format");

        _customerNumberValidatorMock
            .Setup(x => x.Validate(It.IsAny<string?>()))
            .Returns(new OperationResponse<long, ValidationFailure>.FailedOperation(failure));

        // Act
        var result = _sut.Validate(request);

        // Assert
        var resultFailure =
            Assert.IsType<OperationResponse<ValidatedCreateCustomerRequest, ValidationFailure>.FailedOperation>(result);
        Assert.Equal(failure, resultFailure.Data);

        // Senior Check: Ensure we didn't waste cycles validating names if the ID failed
        _firstNameValidatorMock.Verify(x => x.Validate(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void Validate_WhenCustomerNameIsNull_ReturnsFailure()
    {
        // Arrange
        var request = new CreateCustomerRequest { CustomerNumber = "123", CustomerName = null };
        SetupCustomerNumberSuccess(123L);

        // Act
        var result = _sut.Validate(request);

        // Assert
        var failure =
            Assert.IsType<OperationResponse<ValidatedCreateCustomerRequest, ValidationFailure>.FailedOperation>(result);
        Assert.Equal(RequestFields.CustomerName, failure.Data.Field);
        Assert.Equal("The customer name is required", failure.Data.ErrorMessage);
    }

    [Fact]
    public void Validate_WhenFirstNameFails_ReturnsFailure()
    {
        // Arrange
        var request = new CreateCustomerRequest
        {
            CustomerNumber = "123",
            CustomerName = new CustomerName { FirstName = "J", LastName = "Doe" }
        };
        var failure = new ValidationFailure(RequestFields.FirstName, "Too short");

        SetupCustomerNumberSuccess(123L);
        _firstNameValidatorMock
            .Setup(x => x.Validate("J"))
            .Returns(new OperationResponse<string, ValidationFailure>.FailedOperation(failure));

        // Act
        var result = _sut.Validate(request);

        // Assert
        var resultFailure =
            Assert.IsType<OperationResponse<ValidatedCreateCustomerRequest, ValidationFailure>.FailedOperation>(result);
        Assert.Equal(failure, resultFailure.Data);
        _lastNameValidatorMock.Verify(x => x.Validate(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void Validate_WhenLastNameFails_ReturnsFailure()
    {
        // Arrange
        var request = new CreateCustomerRequest
        {
            CustomerNumber = "123",
            CustomerName = new CustomerName { FirstName = "John", LastName = "" }
        };
        var failure = new ValidationFailure(RequestFields.LastName, "Required");

        SetupCustomerNumberSuccess(123L);
        SetupFirstNameSuccess("John");
        _lastNameValidatorMock
            .Setup(x => x.Validate(""))
            .Returns(new OperationResponse<string, ValidationFailure>.FailedOperation(failure));

        // Act
        var result = _sut.Validate(request);

        // Assert
        var resultFailure =
            Assert.IsType<OperationResponse<ValidatedCreateCustomerRequest, ValidationFailure>.FailedOperation>(result);
        Assert.Equal(failure, resultFailure.Data);
    }

    #region Helpers

    private void SetupCustomerNumberSuccess(long val)
    {
        _customerNumberValidatorMock.Setup(x => x.Validate(It.IsAny<string?>()))
            .Returns(new OperationResponse<long, ValidationFailure>.SuccessfulOperation(val));
    }

    private void SetupFirstNameSuccess(string val)
    {
        _firstNameValidatorMock.Setup(x => x.Validate(It.IsAny<string?>()))
            .Returns(new OperationResponse<string, ValidationFailure>.SuccessfulOperation(val));
    }

    private void SetupLastNameSuccess(string val)
    {
        _lastNameValidatorMock.Setup(x => x.Validate(It.IsAny<string?>()))
            .Returns(new OperationResponse<string, ValidationFailure>.SuccessfulOperation(val));
    }

    private void VerifyLogContains(string messagePart)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messagePart)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion
}