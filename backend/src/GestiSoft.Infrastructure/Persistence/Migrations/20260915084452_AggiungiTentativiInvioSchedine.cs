using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiTentativiInvioSchedine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProssimoTentativoAtUtc",
                table: "paytourist_strutture",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TentativiFallitiOggi",
                table: "paytourist_strutture",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TentativiGiornoAtUtc",
                table: "paytourist_strutture",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UltimoErroreDefinitivo",
                table: "paytourist_strutture",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProssimoTentativoAtUtc",
                table: "osservatorio_appartamenti",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TentativiFallitiOggi",
                table: "osservatorio_appartamenti",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TentativiGiornoAtUtc",
                table: "osservatorio_appartamenti",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UltimoErroreDefinitivo",
                table: "osservatorio_appartamenti",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProssimoTentativoAtUtc",
                table: "alloggiati_web_integrazioni",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TentativiFallitiOggi",
                table: "alloggiati_web_integrazioni",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TentativiGiornoAtUtc",
                table: "alloggiati_web_integrazioni",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UltimoErroreDefinitivo",
                table: "alloggiati_web_integrazioni",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProssimoTentativoAtUtc",
                table: "paytourist_strutture");

            migrationBuilder.DropColumn(
                name: "TentativiFallitiOggi",
                table: "paytourist_strutture");

            migrationBuilder.DropColumn(
                name: "TentativiGiornoAtUtc",
                table: "paytourist_strutture");

            migrationBuilder.DropColumn(
                name: "UltimoErroreDefinitivo",
                table: "paytourist_strutture");

            migrationBuilder.DropColumn(
                name: "ProssimoTentativoAtUtc",
                table: "osservatorio_appartamenti");

            migrationBuilder.DropColumn(
                name: "TentativiFallitiOggi",
                table: "osservatorio_appartamenti");

            migrationBuilder.DropColumn(
                name: "TentativiGiornoAtUtc",
                table: "osservatorio_appartamenti");

            migrationBuilder.DropColumn(
                name: "UltimoErroreDefinitivo",
                table: "osservatorio_appartamenti");

            migrationBuilder.DropColumn(
                name: "ProssimoTentativoAtUtc",
                table: "alloggiati_web_integrazioni");

            migrationBuilder.DropColumn(
                name: "TentativiFallitiOggi",
                table: "alloggiati_web_integrazioni");

            migrationBuilder.DropColumn(
                name: "TentativiGiornoAtUtc",
                table: "alloggiati_web_integrazioni");

            migrationBuilder.DropColumn(
                name: "UltimoErroreDefinitivo",
                table: "alloggiati_web_integrazioni");
        }
    }
}
