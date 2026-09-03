using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiUltimaVerificaOkIntegrazioniEsterne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaVerificaOkAtUtc",
                table: "paytourist_strutture",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaVerificaOkAtUtc",
                table: "osservatorio_appartamenti",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaVerificaOkAtUtc",
                table: "alloggiati_web_integrazioni",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UltimaVerificaOkAtUtc",
                table: "paytourist_strutture");

            migrationBuilder.DropColumn(
                name: "UltimaVerificaOkAtUtc",
                table: "osservatorio_appartamenti");

            migrationBuilder.DropColumn(
                name: "UltimaVerificaOkAtUtc",
                table: "alloggiati_web_integrazioni");
        }
    }
}
