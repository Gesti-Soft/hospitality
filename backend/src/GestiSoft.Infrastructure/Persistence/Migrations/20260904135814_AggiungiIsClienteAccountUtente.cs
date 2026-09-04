using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiIsClienteAccountUtente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsClienteAccount",
                table: "utenti",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: finora "l'amministratore di un Cliente" era solo un'euristica lato frontend
            // (SuperAdminClientiPage.trovaAdminCliente, "il primo utente creato per quel Cliente"),
            // mai un dato reale. Per non far perdere il proprio titolare a nessun Cliente esistente al
            // deploy, riproduciamo qui la stessa identica euristica: l'utente più vecchio (CreatedAtUtc
            // minimo) per ogni ClienteId diventa il titolare (IsClienteAccount = true).
            migrationBuilder.Sql(
                """
                UPDATE utenti
                SET "IsClienteAccount" = true
                WHERE "Id" IN (
                    SELECT DISTINCT ON ("ClienteId") "Id"
                    FROM utenti
                    WHERE "ClienteId" IS NOT NULL
                    ORDER BY "ClienteId", "CreatedAtUtc" ASC
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsClienteAccount",
                table: "utenti");
        }
    }
}
