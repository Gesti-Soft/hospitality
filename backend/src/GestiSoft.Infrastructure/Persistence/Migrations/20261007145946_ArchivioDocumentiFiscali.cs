using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArchivioDocumentiFiscali : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documenti_fiscali_archiviati",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteRagioneSociale = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StrutturaIdOriginale = table.Column<Guid>(type: "uuid", nullable: false),
                    StrutturaNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StrutturaPartitaIva = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Controparte = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Importo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Pdf = table.Column<byte[]>(type: "bytea", nullable: true),
                    File = table.Column<byte[]>(type: "bytea", nullable: true),
                    NomeFile = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ArchiviatoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Scadenza = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documenti_fiscali_archiviati", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documenti_fiscali_archiviati_ClienteId",
                table: "documenti_fiscali_archiviati",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_documenti_fiscali_archiviati_Scadenza",
                table: "documenti_fiscali_archiviati",
                column: "Scadenza");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documenti_fiscali_archiviati");
        }
    }
}
