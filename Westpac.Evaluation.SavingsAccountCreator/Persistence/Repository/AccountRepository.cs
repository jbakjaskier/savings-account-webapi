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
}

public class AccountRepository(
    AccountDbContext context,
    IOptions<SavingsAccountCreationConfiguration> savingsAccountCreationOptions,
    ILogger<AccountRepository> logger) : IAccountRepository
{
    public async Task<OperationResponse<AccountResponse, ActionFailure>> CreateSavingsAccount(
        ValidatedSavingsAccountRequest account, CancellationToken cancellationToken)
    {
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


            var existingAccountCount = customerInDb.Accounts?.Count;

            //Enforce the maximum limit 
            if (existingAccountCount is not null &&
                existingAccountCount >= savingsAccountCreationOptions.Value.MaxAccountsPerCustomer)
            {
                logger.LogInformation(
                    "The {customerNumber} has exceeded the maximum number of accounts allowed to be created per customer as they have {existingAccountCount} accounts and only {maxAccountPerCustomer} are allowed",
                    account.CustomerNumber, existingAccountCount,
                    savingsAccountCreationOptions.Value.MaxAccountsPerCustomer);

                return new OperationResponse<AccountResponse, ActionFailure>.FailedOperation(
                    new ActionFailure(ErrorCode.ExceededMaxNumberOfAccounts,
                        "The Customer has exceeded the maximum number of accounts allowed to be created per customer"));
            }

            var newAccountId = Guid.NewGuid();

            var newAccount = new Account
            {
                Id = newAccountId,
                CustomerId = customerInDb.CustomerId,
                Customer = customerInDb,
                BankCode = "03",
                BranchCode = account.BranchCode,
                AccountSuffix = ((existingAccountCount ?? 0) + 1).ToString("D3"), // Generates 001, 002, etc.
                AccountNickName = account.AccountNickName
            };

            await context.Accounts.AddAsync(newAccount, cancellationToken);
            
            await context.SaveChangesAsync(cancellationToken); 

            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("The savings account has been successfully created with {accountId}", newAccountId);

            return new OperationResponse<AccountResponse, ActionFailure>.SuccessfulOperation(new AccountResponse
            {
                BankCode = newAccount.BankCode,
                BranchCode = newAccount.BranchCode,
                AccountNumber = newAccount.AccountNumber,
                AccountSuffix = newAccount.AccountSuffix
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
}