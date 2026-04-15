using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Westpac.Evaluation.DomainModels;
using Westpac.Evaluation.SavingsAccountCreator.Configuration;
using Westpac.Evaluation.SavingsAccountCreator.Models;
using Westpac.Evaluation.SavingsAccountCreator.Persistence.Entity;

namespace Westpac.Evaluation.SavingsAccountCreator.Persistence.Repository;

public interface IAccountRepository
{
    public Task<OperationResponse<AccountResponse, ActionFailure>> CreateSavingsAccount(
        ValidatedSavingsAccountRequest account, CancellationToken cancellationToken);


    public Task<OperationResponse<List<AccountResponse>, ActionFailure>> GetAccountsForCustomer(long customerNumber,
        CancellationToken cancellationToken);


    public Task<OperationResponse<CustomerResponse, ActionFailure>> CreateCustomer(
        ValidatedCreateCustomerRequest request, CancellationToken cancellationToken);
}

public class AccountRepository(
    AccountDbContext context,
    IOptions<SavingsAccountCreationConfiguration> savingsAccountCreationOptions,
    ILogger<AccountRepository> logger) : IAccountRepository
{
    public async Task<OperationResponse<AccountResponse, ActionFailure>> CreateSavingsAccount(
        ValidatedSavingsAccountRequest account, CancellationToken cancellationToken)
    {
        // 1. Create the sequence in a separate, isolated step (No explicit transaction, or a short separate one)
        // This way it commits immediately and doesn't lock system catalogs for the duration of the account creation.
        var sequenceName = $"{AccountDbContextConstants.AccountNumberSequenceName}_{account.BranchCode}";
        //var qualifiedSequenceName = $@"""{AccountDbContextConstants.AccountSchemaName}"".""{sequenceName}""";

        //Schema Name - BankAccounts is Hardcode and not used as a constant and reused because EF Core does not support schema names as parameters. 
        // Using ExecuteSqlAsync (EF Core 7+) as it safely parameterizes interpolated strings
        await context.Database.ExecuteSqlAsync(
            $@"CALL ""BankAccounts"".create_account_sequence({sequenceName})",
            cancellationToken);

        await using var transaction =
            await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var customerInDb = await context
                .Customers
                .Include(x => x.Accounts)
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.CustomerNumber == account.CustomerNumber, cancellationToken);

            if (customerInDb is null)
            {
                logger.LogInformation("The {customerNumber} does not exist", account.CustomerNumber);

                return new OperationResponse<AccountResponse, ActionFailure>.FailedOperation(
                    new ActionFailure(ErrorCode.CustomerDoesNotExist, "The Customer Number does not exist")
                );
            }

            var existingAccountCountForCustomer = customerInDb.Accounts?.Count;

            //Enforce the maximum limit 
            if (existingAccountCountForCustomer is not null &&
                existingAccountCountForCustomer.Value >= savingsAccountCreationOptions.Value.MaxAccountsPerCustomer)
            {
                logger.LogInformation(
                    "The {customerNumber} has exceeded the maximum number of accounts allowed to be created per customer as they have {existingAccountCount} accounts and only {maxAccountPerCustomer} are allowed",
                    account.CustomerNumber, existingAccountCountForCustomer,
                    savingsAccountCreationOptions.Value.MaxAccountsPerCustomer);

                return new OperationResponse<AccountResponse, ActionFailure>.FailedOperation(
                    new ActionFailure(ErrorCode.ExceededMaxNumberOfAccountsPerCustomer,
                        "The Customer has exceeded the maximum number of accounts allowed to be created per customer"));
            }

            var createAccountModelForBranch = async () =>
            {
                var isNewBranchCodeForCustomer =
                    customerInDb.Accounts is null ||
                    !existingAccountCountForCustomer.HasValue ||
                    existingAccountCountForCustomer == 0 ||
                    customerInDb.Accounts.All(x => x.BranchCode != account.BranchCode);


                if (isNewBranchCodeForCustomer)
                {
                    // 3. nextval() is perfectly safe inside the transaction. 
                    var newAccountNumberToBeGenerated = await context.Database.SqlQuery<int>(
                            $@"SELECT ""BankAccounts"".get_next_account_number({sequenceName}) AS ""Value""")
                        .SingleAsync(cancellationToken);

                    return new Account
                    {
                        CustomerId = customerInDb.CustomerId,
                        BranchCode = account.BranchCode,
                        AccountNumber = newAccountNumberToBeGenerated.ToString("D7"),
                        AccountSuffix = "000",
                        AccountNickName = account.AccountNickName
                    };
                }

                var latestAccountForCustomerInBranch = customerInDb.Accounts!
                    .Where(x => x.BranchCode == account.BranchCode)
                    .OrderByDescending(x => x.CreatedAt).First();

                var latestAccountSuffixForCustomerInBranch = int.Parse(latestAccountForCustomerInBranch.AccountSuffix);

                return new Account
                {
                    CustomerId = customerInDb.CustomerId,
                    BranchCode = account.BranchCode,
                    AccountNumber = latestAccountForCustomerInBranch.AccountNumber,
                    AccountSuffix =
                        (latestAccountSuffixForCustomerInBranch + 1).ToString("D3"), // Generates 000, 001, 002, etc.
                    AccountNickName = account.AccountNickName
                };
            };

            var newAccountToBeCreated = await createAccountModelForBranch();

            await context.Accounts.AddAsync(newAccountToBeCreated, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("The savings account has been successfully created with {accountId}",
                newAccountToBeCreated.Id);

            return new OperationResponse<AccountResponse, ActionFailure>.SuccessfulOperation(new AccountResponse
            {
                BankCode = newAccountToBeCreated.BankCode,
                BranchCode = newAccountToBeCreated.BranchCode,
                AccountNumber = newAccountToBeCreated.AccountNumber,
                AccountSuffix = newAccountToBeCreated.AccountSuffix
            });
        }

        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);

            logger.LogError(exception, "An error occurred while creating the savings account");

            return new OperationResponse<AccountResponse, ActionFailure>.FailedOperation(
                new ActionFailure(ErrorCode.UnknownFailure,
                    "An error occurred while creating the savings account. Please contact the support team if the issue persists")
            );
        }
    }

    public async Task<OperationResponse<List<AccountResponse>, ActionFailure>> GetAccountsForCustomer(
        long customerNumber, CancellationToken cancellationToken)
    {
        try
        {
            var customerInDb = await context
                .Customers
                .Include(x => x.Accounts)
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.CustomerNumber == customerNumber, cancellationToken);

            if (customerInDb is null)
                return new OperationResponse<List<AccountResponse>, ActionFailure>.FailedOperation(
                    new ActionFailure(ErrorCode.CustomerDoesNotExist, "The Customer Number does not exist")
                );

            if (customerInDb.Accounts is null || customerInDb.Accounts.Count == 0)
                return new OperationResponse<List<AccountResponse>, ActionFailure>.FailedOperation(
                    new ActionFailure(ErrorCode.CustomerDoesNotHaveAnyAccounts,
                        "The Customer Number does not have any accounts")
                );

            var response = customerInDb.Accounts.Select(x => new AccountResponse
            {
                BankCode = x.BankCode,
                BranchCode = x.BranchCode,
                AccountNumber = x.AccountNumber,
                AccountSuffix = x.AccountSuffix
            }).ToList();

            logger.LogInformation("{numberOfAccounts} were retrieved for customer with {customerNumber}",
                response.Count, customerNumber);
            return new OperationResponse<List<AccountResponse>, ActionFailure>.SuccessfulOperation(response);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "An error occured while trying to get accounts for a customer");

            return new OperationResponse<List<AccountResponse>, ActionFailure>.FailedOperation(
                new ActionFailure(ErrorCode.UnknownFailure,
                    "An error occurred while getting the savings account for a customer. Please contact the support team if the issue persists")
            );
        }
    }


    /// <summary>
    ///     This is an idempotent operation.
    ///     If the customer already exists, it will update the name details of the customer.
    ///     If the customer does not exist, it will create the customer and return the customer.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<OperationResponse<CustomerResponse, ActionFailure>> CreateCustomer(
        ValidatedCreateCustomerRequest request, CancellationToken cancellationToken)
    {
        await using var transaction =
            await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            // Using ExecuteSqlAsync (EF Core 7+) as it safely parameterizes interpolated strings
            //The DO EXECUTE ceremony is due to the schema interpolation that is required.
            // NOTE: %I = identifier (schema/table/column), %L = literal (safely quoted value)
            // EF Core parameters ({...}) can't be used inside DO $$ blocks, so all values
            // must be passed through format()'s %L placeholders instead.
            var rowsAffected = await context.Database.ExecuteSqlAsync(
                $@"INSERT INTO ""BankAccounts"".""Customers""
                        (""CustomerNumber"", ""FirstName"", ""LastName"")
                        VALUES ({request.CustomerNumber}, {request.CustomerName.FirstName}, {request.CustomerName.LastName})
                    ON CONFLICT (""CustomerNumber"") 
                    DO UPDATE SET 
                        ""FirstName"" = EXCLUDED.""FirstName"",
                        ""LastName"" = EXCLUDED.""LastName""",
                cancellationToken);


            await transaction.CommitAsync(cancellationToken);

            if (rowsAffected == 0)
            {
                logger.LogError(
                    "The customer was not upserted in the database with {customerNumber} and {firstName} and {lastName}, even though the statement was executed successfully",
                    request.CustomerNumber, request.CustomerName.FirstName, request.CustomerName.LastName);

                return new OperationResponse<CustomerResponse, ActionFailure>.FailedOperation(
                    new ActionFailure(ErrorCode.UnknownFailure,
                        "An error occurred while creating the customer. Please contact the support team if the issue persists")
                );
            }

            logger.LogInformation(
                "The customer was successfully upserted in the database with {customerNumber} and {firstName} and {lastName}",
                request.CustomerNumber, request.CustomerName.FirstName, request.CustomerName.LastName);

            return new OperationResponse<CustomerResponse, ActionFailure>.SuccessfulOperation(new CustomerResponse
            {
                CustomerNumber = request.CustomerNumber
            });
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);

            logger.LogError(exception, "An error occured while trying to create a customer");

            return new OperationResponse<CustomerResponse, ActionFailure>.FailedOperation(
                new ActionFailure(ErrorCode.UnknownFailure,
                    "An error occurred while creating the customer. Please contact the support team if the issue persists")
            );
        }
    }
}