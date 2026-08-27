using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrazionePayTouristFase8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdPaytouristCache",
                table: "wubook_integrazioni",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "paytourist_integrazioni",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: true),
                    PortaleOnlineAttivo = table.Column<bool>(type: "boolean", nullable: false),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paytourist_integrazioni", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paytourist_integrazioni_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "paytourist_strutture",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    IdStrutturaPaytourist = table.Column<int>(type: "integer", nullable: true),
                    UltimoInvioAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UltimeInviate = table.Column<int>(type: "integer", nullable: true),
                    UltimoErrore = table.Column<string>(type: "text", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paytourist_strutture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paytourist_strutture_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "paytourist_strutture_tipologie",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayTouristStrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipologiaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paytourist_strutture_tipologie", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paytourist_strutture_tipologie_paytourist_strutture_PayTour~",
                        column: x => x.PayTouristStrutturaId,
                        principalTable: "paytourist_strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_paytourist_strutture_tipologie_tipologie_camera_TipologiaId",
                        column: x => x.TipologiaId,
                        principalTable: "tipologie_camera",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_paytourist_integrazioni_StrutturaId",
                table: "paytourist_integrazioni",
                column: "StrutturaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paytourist_strutture_StrutturaId",
                table: "paytourist_strutture",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_paytourist_strutture_tipologie_PayTouristStrutturaId_Tipolo~",
                table: "paytourist_strutture_tipologie",
                columns: new[] { "PayTouristStrutturaId", "TipologiaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paytourist_strutture_tipologie_TipologiaId",
                table: "paytourist_strutture_tipologie",
                column: "TipologiaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "paytourist_integrazioni");

            migrationBuilder.DropTable(
                name: "paytourist_strutture_tipologie");

            migrationBuilder.DropTable(
                name: "paytourist_strutture");

            migrationBuilder.DropColumn(
                name: "IdPaytouristCache",
                table: "wubook_integrazioni");
        }
    }
}
