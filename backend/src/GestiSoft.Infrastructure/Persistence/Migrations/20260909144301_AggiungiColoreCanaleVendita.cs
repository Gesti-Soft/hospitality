using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiColoreCanaleVendita : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Colore",
                table: "canali_vendita",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "");

            // Backfill dei canali già esistenti: stessa palette/logica di CanaliVenditaService, un
            // colore diverso per canale in ordine di creazione all'interno di ogni Struttura (non
            // lasciarli vuoti, altrimenti il Calendario non avrebbe un colore da mostrare finché
            // l'utente non li modifica manualmente uno per uno).
            migrationBuilder.Sql(
                """
                WITH numerati AS (
                    SELECT "Id", (ROW_NUMBER() OVER (PARTITION BY "StrutturaId" ORDER BY "CreatedAtUtc", "Id") - 1) AS idx
                    FROM canali_vendita
                )
                UPDATE canali_vendita c
                SET "Colore" = (ARRAY['#1C7EA8','#DD7A2C','#1F8A70','#3A4453','#4FB4DE','#F0A868','#C2921C','#C24444'])[(n.idx % 8) + 1]
                FROM numerati n
                WHERE n."Id" = c."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Colore",
                table: "canali_vendita");
        }
    }
}
