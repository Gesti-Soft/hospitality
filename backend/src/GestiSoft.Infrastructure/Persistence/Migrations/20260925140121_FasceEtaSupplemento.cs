using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FasceEtaSupplemento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<int>>(
                name: "EtaBambini",
                table: "prenotazioni",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.CreateTable(
                name: "fasce_eta_supplemento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipologiaId = table.Column<Guid>(type: "uuid", nullable: false),
                    EtaMin = table.Column<int>(type: "integer", nullable: false),
                    EtaMax = table.Column<int>(type: "integer", nullable: false),
                    ImportoPerNotte = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fasce_eta_supplemento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fasce_eta_supplemento_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fasce_eta_supplemento_tipologie_camera_TipologiaId",
                        column: x => x.TipologiaId,
                        principalTable: "tipologie_camera",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fasce_eta_supplemento_StrutturaId",
                table: "fasce_eta_supplemento",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_fasce_eta_supplemento_TipologiaId",
                table: "fasce_eta_supplemento",
                column: "TipologiaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fasce_eta_supplemento");

            migrationBuilder.DropColumn(
                name: "EtaBambini",
                table: "prenotazioni");
        }
    }
}
