using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Westpac.Evaluation.SavingsAccountCreator.Persistence.Entity;

public class Account
{
    /// <summary>
    ///     This is the unique identifier for the account
    ///     This is internal and NOT exposed to the public
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    ///     This is a static value for Westpac Bank - this maps to 03
    ///     This is a two digit value
    /// </summary>
    public string BankCode { get; init; } = "03";


    /// <summary>
    ///     This is a four digit value assigned based on the branch that the account is opened in within Westpac
    /// </summary>
    public required string BranchCode { get; init; }


    /// <summary>
    ///     This is the account number for the account.
    ///     This is a seven digit value
    /// </summary>
    public required string AccountNumber { get; init; }


    /// <summary>
    ///     This is a three digit value that is appended to the account number based on the number of accounts opened by the
    ///     customer
    ///     For example : 001 - For the first checking account opened by the customer
    ///     For example : 002 - For the second savings account opened by the customer
    ///     If your account suffix has two numbers and you're being asked for three, add a zero before it.
    ///     This data model ONLY accepts 3 digits
    /// </summary>
    public required string AccountSuffix { get; init; }


    /// <summary>
    ///     This is a nickname for the account. This is optional and can be null.
    /// </summary>
    public string? AccountNickName { get; init; }
    
    /// <summary>
    /// This is the date and time that the account was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }


    /// <summary>
    ///     This is the EF Core Foreign Key relationship to the Customer Entity
    ///     A customer SHOULD be associated with an account
    /// </summary>
    public required Guid CustomerId { get; set; }

    /// <summary>
    ///     This is the EF Core Navigation Property to the Customer Entity
    ///     This is a many to one relationship
    /// </summary>
    public Customer Customer { get; set; }
}

public class AccountEntityTypeConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id)
            .HasDefaultValueSql("uuidv7()")
            .ValueGeneratedOnAdd();
        
        builder.Property(x => x.BankCode)
            .HasDefaultValue("03")
            .IsFixedLength()
            .HasMaxLength(2)
            .IsRequired();


        builder.Property(x => x.BranchCode)
            .IsFixedLength()
            .IsRequired()
            .HasMaxLength(4);

        builder.Property(x => x.AccountSuffix)
            .IsRequired()
            .IsFixedLength()
            .HasMaxLength(3);


        builder.Property(x => x.AccountNickName)
            .IsRequired(false)
            .IsFixedLength(false)
            .HasMaxLength(30);
        
        
        // 2. Configure the AccountNumber string property
        builder
            .Property(a => a.AccountNumber)
            .IsRequired()
            .IsFixedLength()
            .HasMaxLength(7);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        
        //The complete acccount number should be unique for every account in the database
        builder.HasIndex(x => new { x.BankCode, x.BranchCode, x.AccountNumber, x.AccountSuffix })
            .IsUnique();
    }
}