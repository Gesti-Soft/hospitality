using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PulizieDuranteSoggiorno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntervalloBiancheriaGiorni",
                table: "tipologie_camera",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntervalloPuliziaGiorni",
                table: "tipologie_camera",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RinunciaBiancheria",
                table: "prenotazioni",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RinunciaPulizia",
                table: "prenotazioni",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaPuliziaSoggiorno",
                table: "prenotazioni",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoCambioBiancheria",
                table: "prenotazioni",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntervalloBiancheriaGiorni",
                table: "impostazioni_struttura",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntervalloPuliziaGiorni",
                table: "impostazioni_struttura",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntervalloBiancheriaGiorni",
                table: "tipologie_camera");

            migrationBuilder.DropColumn(
                name: "IntervalloPuliziaGiorni",
                table: "tipologie_camera");

            migrationBuilder.DropColumn(
                name: "RinunciaBiancheria",
                table: "prenotazioni");

            migrationBuilder.DropColumn(
                name: "RinunciaPulizia",
                table: "prenotazioni");

            migrationBuilder.DropColumn(
                name: "UltimaPuliziaSoggiorno",
                table: "prenotazioni");

            migrationBuilder.DropColumn(
                name: "UltimoCambioBiancheria",
                table: "prenotazioni");

            migrationBuilder.DropColumn(
                name: "IntervalloBiancheriaGiorni",
                table: "impostazioni_struttura");

            migrationBuilder.DropColumn(
                name: "IntervalloPuliziaGiorni",
                table: "impostazioni_struttura");
        }
    }
}
