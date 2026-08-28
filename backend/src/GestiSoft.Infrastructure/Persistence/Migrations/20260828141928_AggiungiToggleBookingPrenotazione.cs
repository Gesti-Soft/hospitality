using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiToggleBookingPrenotazione : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nota: "DisattivataAtUtc" su strutture NON compare qui — è già stata aggiunta e
            // applicata dalla migration AggiungiDisattivataAtStruttura (Fase 11), il diff l'ha
            // rilevata di nuovo solo perché quella migration non era ancora committata in git
            // quando è stata rigenerata questa; rimossa manualmente per non tentare un ADD COLUMN
            // su una colonna già esistente nel database.
            migrationBuilder.AddColumn<bool>(
                name: "CauzioneAttiva",
                table: "prenotazioni",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SpesePuliziaAttiva",
                table: "prenotazioni",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "TassaSoggiornoAttiva",
                table: "prenotazioni",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CauzioneAttiva",
                table: "prenotazioni");

            migrationBuilder.DropColumn(
                name: "SpesePuliziaAttiva",
                table: "prenotazioni");

            migrationBuilder.DropColumn(
                name: "TassaSoggiornoAttiva",
                table: "prenotazioni");
        }
    }
}
