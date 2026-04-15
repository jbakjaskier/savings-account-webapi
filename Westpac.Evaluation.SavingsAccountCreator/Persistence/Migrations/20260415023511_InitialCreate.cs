using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westpac.Evaluation.SavingsAccountCreator.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "BankAccounts");

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "BankAccounts",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    CustomerNumber = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.CustomerId);
                });

            migrationBuilder.CreateTable(
                name: "Accounts",
                schema: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    BankCode = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false, defaultValue: "03"),
                    BranchCode = table.Column<string>(type: "character(4)", fixedLength: true, maxLength: 4, nullable: false),
                    AccountNumber = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false),
                    AccountSuffix = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    AccountNickName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Accounts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "BankAccounts",
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_BankCode_BranchCode_AccountNumber_AccountSuffix",
                schema: "BankAccounts",
                table: "Accounts",
                columns: new[] { "BankCode", "BranchCode", "AccountNumber", "AccountSuffix" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CustomerId",
                schema: "BankAccounts",
                table: "Accounts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomerNumber",
                schema: "BankAccounts",
                table: "Customers",
                column: "CustomerNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Accounts",
                schema: "BankAccounts");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "BankAccounts");
        }
    }
}
