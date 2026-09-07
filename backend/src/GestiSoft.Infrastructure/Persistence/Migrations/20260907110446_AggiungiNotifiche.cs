using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiNotifiche : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notifiche",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Titolo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Messaggio = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PrenotazioneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Canale = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Stato = table.Column<int>(type: "integer", nullable: false),
                    ScadenzaAttesaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LettaAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChiaveDedup = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifiche", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifiche_prenotazioni_PrenotazioneId",
                        column: x => x.PrenotazioneId,
                        principalTable: "prenotazioni",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_notifiche_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notifiche_PrenotazioneId",
                table: "notifiche",
                column: "PrenotazioneId");

            migrationBuilder.CreateIndex(
                name: "IX_notifiche_StrutturaId",
                table: "notifiche",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_notifiche_StrutturaId_ChiaveDedup",
                table: "notifiche",
                columns: new[] { "StrutturaId", "ChiaveDedup" },
                filter: "\"ChiaveDedup\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notifiche_StrutturaId_Stato_LettaAtUtc",
                table: "notifiche",
                columns: new[] { "StrutturaId", "Stato", "LettaAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifiche");
        }
    }
}
