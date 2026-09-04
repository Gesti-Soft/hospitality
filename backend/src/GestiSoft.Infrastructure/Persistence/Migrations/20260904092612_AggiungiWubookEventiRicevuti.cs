using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiWubookEventiRicevuti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wubook_eventi_ricevuti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Lcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Rcode = table.Column<int>(type: "integer", nullable: false),
                    ImportazioneRiuscita = table.Column<bool>(type: "boolean", nullable: false),
                    MessaggioErrore = table.Column<string>(type: "text", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wubook_eventi_ricevuti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wubook_eventi_ricevuti_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wubook_eventi_ricevuti_StrutturaId",
                table: "wubook_eventi_ricevuti",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_wubook_eventi_ricevuti_StrutturaId_Rcode",
                table: "wubook_eventi_ricevuti",
                columns: new[] { "StrutturaId", "Rcode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wubook_eventi_ricevuti");
        }
    }
}
