using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SpostaTokenWubookInImpostazioniGlobali : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TokenWubook",
                table: "impostazioni_globali",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // Il Token Wubook era lo stesso identico valore su tutte le Strutture reali (unico
            // account partner Wubook, confermato sui dati prima di scrivere questa migrazione) — ne
            // basta uno per popolare la nuova colonna globale. Se non esiste ancora nessuna riga in
            // impostazioni_globali (caso non reale oggi, solo di sicurezza) la crea.
            migrationBuilder.Sql(
                "INSERT INTO impostazioni_globali (\"Id\", \"TokenWubook\") " +
                "SELECT gen_random_uuid(), (SELECT \"TokenWubook\" FROM wubook_integrazioni WHERE \"TokenWubook\" IS NOT NULL LIMIT 1) " +
                "WHERE NOT EXISTS (SELECT 1 FROM impostazioni_globali);");
            migrationBuilder.Sql(
                "UPDATE impostazioni_globali SET \"TokenWubook\" = " +
                "(SELECT \"TokenWubook\" FROM wubook_integrazioni WHERE \"TokenWubook\" IS NOT NULL LIMIT 1) " +
                "WHERE \"TokenWubook\" IS NULL;");

            migrationBuilder.DropColumn(
                name: "TokenWubook",
                table: "wubook_integrazioni");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TokenWubook",
                table: "impostazioni_globali");

            migrationBuilder.AddColumn<string>(
                name: "TokenWubook",
                table: "wubook_integrazioni",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
