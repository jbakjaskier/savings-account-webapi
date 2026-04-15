using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westpac.Evaluation.SavingsAccountCreator.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredProcToGenerateSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
                CREATE OR REPLACE PROCEDURE ""BankAccounts"".{AccountDbContextConstants.AccountNumberSequenceGeneratorStoredProcedureName}(
                    p_sequence_name text
                )
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    EXECUTE format(
                        'CREATE SEQUENCE IF NOT EXISTS ""BankAccounts"".%I START 0 MINVALUE 0 MAXVALUE 9999999 NO CYCLE',
                        p_sequence_name
                    );
                END;
                $$;

                CREATE OR REPLACE FUNCTION ""BankAccounts"".{AccountDbContextConstants.GetNextAccountNumberStoredProcedureName}(seq_name text)
                    RETURNS integer 
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RETURN nextval(('""BankAccounts""' || '.' || quote_ident(seq_name))::regclass);
                END;
                $$;
            ");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.Sql($@"
                DROP PROCEDURE IF EXISTS ""BankAccounts"".{AccountDbContextConstants.AccountNumberSequenceGeneratorStoredProcedureName};
                DROP FUNCTION IF EXISTS ""BankAccounts"".{AccountDbContextConstants.GetNextAccountNumberStoredProcedureName};
            ");

        }
    }
}
