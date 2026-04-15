using Microsoft.Extensions.Logging;
using Moq;
using Westpac.Evaluation.DomainModels;
using Westpac.Evaluation.SavingsAccountCreator.Models;
using Westpac.Evaluation.SavingsAccountCreator.Validation;

namespace Westpac.Evaluation.Testing.Unit.SavingsAccountCreator.RequestValidatorTests;

public class CustomerNumberValidatorTests
{
    private readonly Mock<ILogger<CustomerNumberValidator>> _loggerMock;
    private readonly CustomerNumberValidator _sut;

    public CustomerNumberValidatorTests()
    {
        _loggerMock = new Mock<ILogger<CustomerNumberValidator>>();
        _sut = new CustomerNumberValidator(_loggerMock.Object);
    }

    [Fact]
    public void Validate_NullInput_ReturnsRequiredFailure()
    {
        var result = _sut.Validate(null);

        var failure = Assert.IsType<OperationResponse<long, ValidationFailure>.FailedOperation>(result);
        Assert.Equal(RequestFields.CustomerNumber, failure.Data.Field);
        Assert.Equal("The customer number is required", failure.Data.ErrorMessage);
    }

    [Fact]
    public void Validate_EmptyString_ReturnsRequiredFailure()
    {
        var result = _sut.Validate(string.Empty);

        var failure = Assert.IsType<OperationResponse<long, ValidationFailure>.FailedOperation>(result);
        Assert.Equal("The customer number is required", failure.Data.ErrorMessage);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Validate_WhitespaceOnly_ReturnsRequiredFailure(string input)
    {
        var result = _sut.Validate(input);

        var failure = Assert.IsType<OperationResponse<long, ValidationFailure>.FailedOperation>(result);
        Assert.Equal("The customer number is required", failure.Data.ErrorMessage);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12a3")]
    [InlineData("12.3")]
    [InlineData("-1")] // negative sign is not a digit
    [InlineData("+123")] // leading plus
    [InlineData("1 2 3")] // embedded spaces
    [InlineData("12e4")] // scientific notation
    [InlineData("①②③")] // Unicode digit-like chars that aren't char.IsDigit ASCII
    public void Validate_NonNumericInput_ReturnsInvalidFailure(string input)
    {
        var result = _sut.Validate(input);

        var failure = Assert.IsType<OperationResponse<long, ValidationFailure>.FailedOperation>(result);
        Assert.Equal(RequestFields.CustomerNumber, failure.Data.Field);
        Assert.Equal("The customer number is invalid", failure.Data.ErrorMessage);
    }

    [Fact]
    public void Validate_NumberExceedingLongMaxValue_ReturnsInvalidFailure()
    {
        // 19 nines — all digits but overflows long.TryParse
        var input = new string('9', 19);

        var result = _sut.Validate(input);

        var failure = Assert.IsType<OperationResponse<long, ValidationFailure>.FailedOperation>(result);
        Assert.Equal("The customer number is invalid", failure.Data.ErrorMessage);
    }


    [Theory]
    [InlineData("1", 1L)]
    [InlineData("0", 0L)]
    [InlineData("123456", 123456L)]
    [InlineData("9223372036854775807", long.MaxValue)] // long.MaxValue boundary
    public void Validate_ValidNumericString_ReturnsParsedLong(string input, long expected)
    {
        var result = _sut.Validate(input);

        var success = Assert.IsType<OperationResponse<long, ValidationFailure>.SuccessfulOperation>(result);
        Assert.Equal(expected, success.Data);
    }

    [Fact]
    public void Validate_AnyInput_LogsEntryInformation()
    {
        _sut.Validate("123");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Validating customer number")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Validate_ValidInput_LogsSuccessInformation()
    {
        _sut.Validate("42");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("validation was successful")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Validate_InvalidInput_DoesNotLogSuccess()
    {
        _sut.Validate("abc");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("validation was successful")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void Validate_NullInput_DoesNotLogSuccess()
    {
        _sut.Validate(null);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("validation was successful")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }


    [Fact]
    public void Validate_ValidInput_ResultIsNotFailedOperation()
    {
        var result = _sut.Validate("999");

        Assert.IsNotType<OperationResponse<long, ValidationFailure>.FailedOperation>(result);
    }

    [Fact]
    public void Validate_InvalidInput_ResultIsNotSuccessfulOperation()
    {
        var result = _sut.Validate("bad");

        Assert.IsNotType<OperationResponse<long, ValidationFailure>.SuccessfulOperation>(result);
    }
}