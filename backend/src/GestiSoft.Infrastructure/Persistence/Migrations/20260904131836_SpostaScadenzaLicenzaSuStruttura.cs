using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SpostaScadenzaLicenzaSuStruttura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ScadenzaLicenza",
                table: "strutture",
                type: "timestamp with time zone",
                nullable: true);

            // Trasferisce i valori già presenti prima di droppare la vecchia colonna: la scadenza non
            // era mai stata la licenza Wubook in senso stretto, ma la licenza software GestiSoft della
            // Struttura — spostarla di tabella non deve azzerare quanto già assegnato dal Super Admin.
            migrationBuilder.Sql(
                """
                UPDATE strutture s
                SET "ScadenzaLicenza" = w."ScadenzaLicenza"
                FROM wubook_integrazioni w
                WHERE w."StrutturaId" = s."Id";
                """);

            migrationBuilder.DropColumn(
                name: "ScadenzaLicenza",
                table: "wubook_integrazioni");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ScadenzaLicenza",
                table: "wubook_integrazioni",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE wubook_integrazioni w
                SET "ScadenzaLicenza" = s."ScadenzaLicenza"
                FROM strutture s
                WHERE s."Id" = w."StrutturaId";
                """);

            migrationBuilder.DropColumn(
                name: "ScadenzaLicenza",
                table: "strutture");
        }
    }
}
