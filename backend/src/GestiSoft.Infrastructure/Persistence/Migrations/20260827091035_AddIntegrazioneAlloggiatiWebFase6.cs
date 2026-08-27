using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrazioneAlloggiatiWebFase6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alloggiati_web_integrazioni",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Utente = table.Column<string>(type: "text", nullable: true),
                    Password = table.Column<string>(type: "text", nullable: true),
                    WsKey = table.Column<string>(type: "text", nullable: true),
                    UltimoInvioAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UltimeSchedineInviate = table.Column<int>(type: "integer", nullable: true),
                    UltimoErrore = table.Column<string>(type: "text", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alloggiati_web_integrazioni", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alloggiati_web_integrazioni_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alloggiati_web_integrazioni_StrutturaId",
                table: "alloggiati_web_integrazioni",
                column: "StrutturaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alloggiati_web_integrazioni");
        }
    }
}
