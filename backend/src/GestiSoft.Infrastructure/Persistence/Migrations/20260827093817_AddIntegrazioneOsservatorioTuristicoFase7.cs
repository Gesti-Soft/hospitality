using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrazioneOsservatorioTuristicoFase7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "osservatorio_appartamenti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    EntityCode = table.Column<string>(type: "text", nullable: true),
                    Password = table.Column<string>(type: "text", nullable: true),
                    HotelCode = table.Column<string>(type: "text", nullable: true),
                    CursoreDataAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProssimoStayIdProgressivo = table.Column<int>(type: "integer", nullable: false),
                    UltimoInvioAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UltimeSchedineInviate = table.Column<int>(type: "integer", nullable: true),
                    UltimoErrore = table.Column<string>(type: "text", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_osservatorio_appartamenti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_osservatorio_appartamenti_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "osservatorio_invii",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrenotazioneId = table.Column<Guid>(type: "uuid", nullable: false),
                    OspiteRigaId = table.Column<Guid>(type: "uuid", nullable: true),
                    StayId = table.Column<string>(type: "text", nullable: false),
                    GuestId = table.Column<string>(type: "text", nullable: false),
                    DataInvioUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_osservatorio_invii", x => x.Id);
                    table.ForeignKey(
                        name: "FK_osservatorio_invii_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "osservatorio_appartamenti_tipologie",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OsservatorioAppartamentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipologiaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_osservatorio_appartamenti_tipologie", x => x.Id);
                    table.ForeignKey(
                        name: "FK_osservatorio_appartamenti_tipologie_osservatorio_appartamen~",
                        column: x => x.OsservatorioAppartamentoId,
                        principalTable: "osservatorio_appartamenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_osservatorio_appartamenti_tipologie_tipologie_camera_Tipolo~",
                        column: x => x.TipologiaId,
                        principalTable: "tipologie_camera",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_osservatorio_appartamenti_StrutturaId",
                table: "osservatorio_appartamenti",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_osservatorio_appartamenti_tipologie_OsservatorioAppartament~",
                table: "osservatorio_appartamenti_tipologie",
                columns: new[] { "OsservatorioAppartamentoId", "TipologiaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_osservatorio_appartamenti_tipologie_TipologiaId",
                table: "osservatorio_appartamenti_tipologie",
                column: "TipologiaId");

            migrationBuilder.CreateIndex(
                name: "IX_osservatorio_invii_PrenotazioneId",
                table: "osservatorio_invii",
                column: "PrenotazioneId");

            migrationBuilder.CreateIndex(
                name: "IX_osservatorio_invii_StrutturaId",
                table: "osservatorio_invii",
                column: "StrutturaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "osservatorio_appartamenti_tipologie");

            migrationBuilder.DropTable(
                name: "osservatorio_invii");

            migrationBuilder.DropTable(
                name: "osservatorio_appartamenti");
        }
    }
}
