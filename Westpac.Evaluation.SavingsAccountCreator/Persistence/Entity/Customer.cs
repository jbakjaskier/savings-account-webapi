using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Westpac.Evaluation.SavingsAccountCreator.Persistence.Entity;

public class Customer
{
    /// <summary>
    /// This is the public facing account number for the customer.
    /// This is exposed to the public and used via different channels - Mobile Banking, Web, InPerson etc. 
    /// </summary>
    public required long CustomerNumber { get; set; }
    
    
    /// <summary>
    /// This is the internal identifier for the customer
    /// This is not exposed to the public
    /// </summary>
    public required Guid CustomerId { get; set; }
    
    /// <summary>
    /// This is the firstname of the customer
    /// </summary>
    public required string FirstName { get; set; }
    
    
    /// <summary>
    /// This is the lastname of the customer
    /// </summary>
    public required string LastName { get; set; }
    
    
    /// <summary>
    /// This is an EF Core Foreign Key relationship to the Account Entity
    /// This is a collection of accounts that the customer has opened
    /// </summary>
    public ICollection<Account>? Accounts { get; set; }
    
}


public class CustomerEntityTypeConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(x => x.CustomerId);

        builder.Property(x => x.CustomerNumber)
            .IsRequired();
        
        //TODO: The first name and last name length restraints as mentioned in the application startup STILL exist here and will need to updated in the DB contract when answered
        builder.Property(x => x.FirstName)
            .IsRequired();

        builder.Property(x => x.LastName)
            .IsRequired();
        
        builder.HasMany(x => x.Accounts)
            .WithOne(x => x.Customer)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}