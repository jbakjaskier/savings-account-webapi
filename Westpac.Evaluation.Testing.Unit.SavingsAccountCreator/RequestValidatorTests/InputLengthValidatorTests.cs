using Microsoft.Extensions.Logging;
using Moq;
using Westpac.Evaluation.DomainModels;
using Westpac.Evaluation.SavingsAccountCreator.Models;
using Westpac.Evaluation.SavingsAccountCreator.Validation;

namespace Westpac.Evaluation.Testing.Unit.SavingsAccountCreator.RequestValidatorTests;

public class InputLengthAndNonEmptyStringValidatorTests
{
    private const int MinLength = 3;
    private const int MaxLength = 10;
    private const RequestFields TestField = RequestFields.FirstName;
    private readonly Mock<ILogger<InputLengthAndNonEmptyStringValidator>> _loggerMock;

    public InputLengthAndNonEmptyStringValidatorTests()
    {
        _loggerMock = new Mock<ILogger<InputLengthAndNonEmptyStringValidator>>();
    }

    private InputLengthAndNonEmptyStringValidator CreateSut()
    {
        return new InputLengthAndNonEmptyStringValidator(_loggerMock.Object, MinLength, MaxLength, TestField);
    }

    [Fact]
    public void Validate_WhenInputIsValid_ReturnsSuccessfulOperation()
    {
        // Arrange
        var sut = CreateSut();
        var input = "ValidName";

        // Act
        var result = sut.Validate(input);

        // Assert
        var success = Assert.IsType<OperationResponse<string, ValidationFailure>.SuccessfulOperation>(result);
        Assert.Equal(input, success.Data);

        // Verify Success Logging
        VerifyLoggerWasCalled(LogLevel.Information, "String length validation was successful");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenInputIsIsNullOrWhitespace_ReturnsRequiredFailure(string? input)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = sut.Validate(input);

        // Assert
        var failure = Assert.IsType<OperationResponse<string, ValidationFailure>.FailedOperation>(result);
        Assert.Equal(TestField, failure.Data.Field);
        Assert.Contains("is required", failure.Data.ErrorMessage);
    }

    [Theory]
    [InlineData("ab")] // 1 char below min
    [InlineData("a")] // significantly below
    public void Validate_WhenInputTooShort_ReturnsTooShortFailure(string input)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = sut.Validate(input);

        // Assert
        var failure = Assert.IsType<OperationResponse<string, ValidationFailure>.FailedOperation>(result);
        Assert.Contains($"at least {MinLength} characters", failure.Data.ErrorMessage);
    }

    [Theory]
    [InlineData("ThisStringIsWayTooLong")] // Over max
    [InlineData("ElevenChars")] // 1 char over max
    public void Validate_WhenInputTooLong_ReturnsTooLongFailure(string input)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = sut.Validate(input);

        // Assert
        var failure = Assert.IsType<OperationResponse<string, ValidationFailure>.FailedOperation>(result);
        Assert.Contains($"cannot be more than {MaxLength} characters", failure.Data.ErrorMessage);
    }

    [Fact]
    public void Validate_AtExactMinBoundary_ReturnsSuccess()
    {
        var sut = CreateSut();
        var result = sut.Validate(new string('a', MinLength));
        Assert.IsType<OperationResponse<string, ValidationFailure>.SuccessfulOperation>(result);
    }

    [Fact]
    public void Validate_AtExactMaxBoundary_ReturnsSuccess()
    {
        var sut = CreateSut();
        var result = sut.Validate(new string('a', MaxLength));
        Assert.IsType<OperationResponse<string, ValidationFailure>.SuccessfulOperation>(result);
    }

    private void VerifyLoggerWasCalled(LogLevel level, string messagePart)
    {
        _loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messagePart)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}