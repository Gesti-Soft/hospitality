using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RinominaQuotaAnnuaAggiungiRinnoviLicenza : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "QuotaMensile",
                table: "clienti",
                newName: "QuotaAnnua");

            migrationBuilder.CreateTable(
                name: "rinnovi_licenza",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScadenzaImpostata = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Importo = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rinnovi_licenza", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rinnovi_licenza_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rinnovi_licenza_StrutturaId",
                table: "rinnovi_licenza",
                column: "StrutturaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rinnovi_licenza");

            migrationBuilder.RenameColumn(
                name: "QuotaAnnua",
                table: "clienti",
                newName: "QuotaMensile");
        }
    }
}
