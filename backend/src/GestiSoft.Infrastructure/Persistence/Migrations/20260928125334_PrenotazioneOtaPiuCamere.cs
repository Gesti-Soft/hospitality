using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrenotazioneOtaPiuCamere : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_prenotazioni_StrutturaId_IdPrenotazioneWubook",
                table: "prenotazioni");

            migrationBuilder.AddColumn<int>(
                name: "IndiceCameraOta",
                table: "prenotazioni",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_prenotazioni_StrutturaId_IdPrenotazioneWubook_IndiceCameraO~",
                table: "prenotazioni",
                columns: new[] { "StrutturaId", "IdPrenotazioneWubook", "IndiceCameraOta" },
                unique: true,
                filter: "\"IdPrenotazioneWubook\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_prenotazioni_StrutturaId_IdPrenotazioneWubook_IndiceCameraO~",
                table: "prenotazioni");

            migrationBuilder.DropColumn(
                name: "IndiceCameraOta",
                table: "prenotazioni");

            migrationBuilder.CreateIndex(
                name: "IX_prenotazioni_StrutturaId_IdPrenotazioneWubook",
                table: "prenotazioni",
                columns: new[] { "StrutturaId", "IdPrenotazioneWubook" },
                unique: true,
                filter: "\"IdPrenotazioneWubook\" IS NOT NULL");
        }
    }
}
