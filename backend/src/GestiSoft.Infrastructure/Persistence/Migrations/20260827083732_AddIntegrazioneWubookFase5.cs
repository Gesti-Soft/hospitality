using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrazioneWubookFase5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdPrenotazioneWubook",
                table: "prenotazioni",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdCameraWubook",
                table: "camere",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WubookAttiva",
                table: "camere",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "wubook_integrazioni",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Attivo = table.Column<bool>(type: "boolean", nullable: false),
                    GestisoftUsername = table.Column<string>(type: "text", nullable: true),
                    GestisoftToken = table.Column<string>(type: "text", nullable: true),
                    ApiKeyCache = table.Column<string>(type: "text", nullable: true),
                    LcodeCache = table.Column<string>(type: "text", nullable: true),
                    CacheAggiornataAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UltimoErrore = table.Column<string>(type: "text", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wubook_integrazioni", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wubook_integrazioni_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prenotazioni_StrutturaId_IdPrenotazioneWubook",
                table: "prenotazioni",
                columns: new[] { "StrutturaId", "IdPrenotazioneWubook" },
                unique: true,
                filter: "\"IdPrenotazioneWubook\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_camere_StrutturaId_IdCameraWubook",
                table: "camere",
                columns: new[] { "StrutturaId", "IdCameraWubook" });

            migrationBuilder.CreateIndex(
                name: "IX_wubook_integrazioni_StrutturaId",
                table: "wubook_integrazioni",
                column: "StrutturaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wubook_integrazioni");

            migrationBuilder.DropIndex(
                name: "IX_prenotazioni_StrutturaId_IdPrenotazioneWubook",
                table: "prenotazioni");

            migrationBuilder.DropIndex(
                name: "IX_camere_StrutturaId_IdCameraWubook",
                table: "camere");

            migrationBuilder.DropColumn(
                name: "IdPrenotazioneWubook",
                table: "prenotazioni");

            migrationBuilder.DropColumn(
                name: "IdCameraWubook",
                table: "camere");

            migrationBuilder.DropColumn(
                name: "WubookAttiva",
                table: "camere");
        }
    }
}
