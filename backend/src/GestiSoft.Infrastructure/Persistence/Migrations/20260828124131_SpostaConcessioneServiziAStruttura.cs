using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SpostaConcessioneServiziAStruttura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AlloggiatiWebAbilitato",
                table: "strutture",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OsservatorioAbilitato",
                table: "strutture",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PayTouristAbilitato",
                table: "strutture",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WubookAbilitato",
                table: "strutture",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill dalle colonne del Cliente PRIMA di rimuoverle: le Strutture già esistenti
            // devono conservare esattamente i servizi già concessi dal Super Admin al loro Cliente
            // (non partire tutte spente) — solo le Strutture create da qui in avanti nasceranno
            // spente di default, come richiesto esplicitamente dall'utente.
            migrationBuilder.Sql("""
                UPDATE strutture s
                SET "WubookAbilitato" = c."WubookAbilitato",
                    "AlloggiatiWebAbilitato" = c."AlloggiatiWebAbilitato",
                    "OsservatorioAbilitato" = c."OsservatorioAbilitato",
                    "PayTouristAbilitato" = c."PayTouristAbilitato"
                FROM clienti c
                WHERE c."Id" = s."ClienteId";
                """);

            migrationBuilder.DropColumn(
                name: "AlloggiatiWebAbilitato",
                table: "clienti");

            migrationBuilder.DropColumn(
                name: "OsservatorioAbilitato",
                table: "clienti");

            migrationBuilder.DropColumn(
                name: "PayTouristAbilitato",
                table: "clienti");

            migrationBuilder.DropColumn(
                name: "WubookAbilitato",
                table: "clienti");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AlloggiatiWebAbilitato",
                table: "clienti",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "OsservatorioAbilitato",
                table: "clienti",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PayTouristAbilitato",
                table: "clienti",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "WubookAbilitato",
                table: "clienti",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Best-effort: un Cliente torna abilitato se ALMENO UNA delle sue Strutture lo era —
            // non è un'inversione perfetta (l'informazione per-struttura si perde tornando indietro),
            // ma evita di spegnere di colpo un Cliente che aveva il servizio concesso da qualche parte.
            migrationBuilder.Sql("""
                UPDATE clienti c
                SET "WubookAbilitato" = COALESCE(agg."WubookAbilitato", true),
                    "AlloggiatiWebAbilitato" = COALESCE(agg."AlloggiatiWebAbilitato", true),
                    "OsservatorioAbilitato" = COALESCE(agg."OsservatorioAbilitato", true),
                    "PayTouristAbilitato" = COALESCE(agg."PayTouristAbilitato", true)
                FROM (
                    SELECT "ClienteId",
                        bool_or("WubookAbilitato") AS "WubookAbilitato",
                        bool_or("AlloggiatiWebAbilitato") AS "AlloggiatiWebAbilitato",
                        bool_or("OsservatorioAbilitato") AS "OsservatorioAbilitato",
                        bool_or("PayTouristAbilitato") AS "PayTouristAbilitato"
                    FROM strutture
                    GROUP BY "ClienteId"
                ) agg
                WHERE agg."ClienteId" = c."Id";
                """);

            migrationBuilder.DropColumn(
                name: "AlloggiatiWebAbilitato",
                table: "strutture");

            migrationBuilder.DropColumn(
                name: "OsservatorioAbilitato",
                table: "strutture");

            migrationBuilder.DropColumn(
                name: "PayTouristAbilitato",
                table: "strutture");

            migrationBuilder.DropColumn(
                name: "WubookAbilitato",
                table: "strutture");
        }
    }
}
