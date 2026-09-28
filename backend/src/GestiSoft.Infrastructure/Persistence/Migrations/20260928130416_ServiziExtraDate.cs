using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ServiziExtraDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_prenotazioni_servizi_PrenotazioneId_ServizioId",
                table: "prenotazioni_servizi");

            migrationBuilder.AddColumn<string>(
                name: "AggiuntoDa",
                table: "prenotazioni_servizi",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "Al",
                table: "prenotazioni_servizi",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "Dal",
                table: "prenotazioni_servizi",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<int>(
                name: "Origine",
                table: "prenotazioni_servizi",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_prenotazioni_servizi_PrenotazioneId",
                table: "prenotazioni_servizi",
                column: "PrenotazioneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_prenotazioni_servizi_PrenotazioneId",
                table: "prenotazioni_servizi");

            migrationBuilder.DropColumn(
                name: "AggiuntoDa",
                table: "prenotazioni_servizi");

            migrationBuilder.DropColumn(
                name: "Al",
                table: "prenotazioni_servizi");

            migrationBuilder.DropColumn(
                name: "Dal",
                table: "prenotazioni_servizi");

            migrationBuilder.DropColumn(
                name: "Origine",
                table: "prenotazioni_servizi");

            migrationBuilder.CreateIndex(
                name: "IX_prenotazioni_servizi_PrenotazioneId_ServizioId",
                table: "prenotazioni_servizi",
                columns: new[] { "PrenotazioneId", "ServizioId" },
                unique: true);
        }
    }
}
