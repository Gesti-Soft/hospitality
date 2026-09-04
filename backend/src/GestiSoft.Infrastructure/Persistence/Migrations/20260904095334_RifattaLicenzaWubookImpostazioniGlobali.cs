using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RifattaLicenzaWubookImpostazioniGlobali : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Le colonne nuove nascono prima di droppare le vecchie: il valore già presente in cache
            // (recuperato da gestisoft.it l'ultima volta che ha funzionato) diventa il punto di
            // partenza delle nuove credenziali dirette, così le Strutture già configurate non restano
            // improvvisamente senza sincronizzazione Wubook finché il Super Admin non le rivede.
            migrationBuilder.AddColumn<string>(
                name: "CodiceStruttura",
                table: "wubook_integrazioni",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenWubook",
                table: "wubook_integrazioni",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE wubook_integrazioni SET \"TokenWubook\" = \"ApiKeyCache\", \"CodiceStruttura\" = \"LcodeCache\";");

            migrationBuilder.CreateTable(
                name: "impostazioni_globali",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdSoftwarePaytourist = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_impostazioni_globali", x => x.Id);
                });

            // IdPaytouristCache era già un unico valore condiviso da tutte le Strutture (confermato
            // sui dati reali prima di scrivere questa migrazione): ne basta uno per popolare la nuova
            // impostazione globale, non serve nessuna scelta arbitraria tra Strutture diverse.
            migrationBuilder.Sql(
                "INSERT INTO impostazioni_globali (\"Id\", \"IdSoftwarePaytourist\") " +
                "SELECT gen_random_uuid(), \"IdPaytouristCache\"::int FROM wubook_integrazioni " +
                "WHERE \"IdPaytouristCache\" ~ '^[0-9]+$' LIMIT 1;");

            migrationBuilder.DropColumn(
                name: "ApiKeyCache",
                table: "wubook_integrazioni");

            migrationBuilder.DropColumn(
                name: "IdPaytouristCache",
                table: "wubook_integrazioni");

            migrationBuilder.DropColumn(
                name: "LcodeCache",
                table: "wubook_integrazioni");

            migrationBuilder.RenameColumn(
                name: "CacheAggiornataAtUtc",
                table: "wubook_integrazioni",
                newName: "ScadenzaLicenza");

            // CacheAggiornataAtUtc era "quando è stata rinnovata l'ultima volta", non una scadenza:
            // portare quel valore così com'è nella colonna rinominata farebbe risultare scadute da
            // subito tutte le licenze già configurate. Nessuna scadenza finché il Super Admin non ne
            // assegna una esplicitamente (null = mai in scadenza, comportamento equivalente a prima).
            migrationBuilder.Sql("UPDATE wubook_integrazioni SET \"ScadenzaLicenza\" = NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "impostazioni_globali");

            migrationBuilder.DropColumn(
                name: "CodiceStruttura",
                table: "wubook_integrazioni");

            migrationBuilder.DropColumn(
                name: "TokenWubook",
                table: "wubook_integrazioni");

            migrationBuilder.RenameColumn(
                name: "ScadenzaLicenza",
                table: "wubook_integrazioni",
                newName: "CacheAggiornataAtUtc");

            migrationBuilder.AddColumn<string>(
                name: "ApiKeyCache",
                table: "wubook_integrazioni",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdPaytouristCache",
                table: "wubook_integrazioni",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LcodeCache",
                table: "wubook_integrazioni",
                type: "text",
                nullable: true);
        }
    }
}
