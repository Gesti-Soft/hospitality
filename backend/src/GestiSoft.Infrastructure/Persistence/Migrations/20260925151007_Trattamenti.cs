using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Trattamenti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Trattamento",
                table: "prenotazioni",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrattamentoEtaMassimaBambini",
                table: "prenotazioni",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TrattamentoPrezzoAdulto",
                table: "prenotazioni",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TrattamentoPrezzoBambino",
                table: "prenotazioni",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "trattamenti_struttura",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Attivo = table.Column<bool>(type: "boolean", nullable: false),
                    PrezzoPerPersona = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PrezzoBambini = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TipoPrezzoBambini = table.Column<int>(type: "integer", nullable: false),
                    EtaMassimaBambini = table.Column<int>(type: "integer", nullable: true),
                    EsercizioConvenzionato = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trattamenti_struttura", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trattamenti_struttura_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trattamenti_struttura_StrutturaId",
                table: "trattamenti_struttura",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_trattamenti_struttura_StrutturaId_Tipo",
                table: "trattamenti_struttura",
                columns: new[] { "StrutturaId", "Tipo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trattamenti_struttura");

            migrationBuilder.DropColumn(
                name: "Trattamento",
                table: "prenotazioni");

            migrationBuilder.DropColumn(
                name: "TrattamentoEtaMassimaBambini",
                table: "prenotazioni");

            migrationBuilder.DropColumn(
                name: "TrattamentoPrezzoAdulto",
                table: "prenotazioni");

            migrationBuilder.DropColumn(
                name: "TrattamentoPrezzoBambino",
                table: "prenotazioni");
        }
    }
}
