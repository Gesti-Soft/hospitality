using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiBloccoLoginE2Fa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BloccatoFinoUtc",
                table: "utenti",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TentativiLoginFalliti",
                table: "utenti",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TotpAttivatoAtUtc",
                table: "utenti",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TotpAttivo",
                table: "utenti",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TotpSecret",
                table: "utenti",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "codici_recupero_utente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UtenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodiceHash = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    UsatoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_codici_recupero_utente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_codici_recupero_utente_utenti_UtenteId",
                        column: x => x.UtenteId,
                        principalTable: "utenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dispositivi_fidati",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UtenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ScadeAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispositivi_fidati", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dispositivi_fidati_utenti_UtenteId",
                        column: x => x.UtenteId,
                        principalTable: "utenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_codici_recupero_utente_UtenteId",
                table: "codici_recupero_utente",
                column: "UtenteId");

            migrationBuilder.CreateIndex(
                name: "IX_dispositivi_fidati_TokenHash",
                table: "dispositivi_fidati",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispositivi_fidati_UtenteId",
                table: "dispositivi_fidati",
                column: "UtenteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "codici_recupero_utente");

            migrationBuilder.DropTable(
                name: "dispositivi_fidati");

            migrationBuilder.DropColumn(
                name: "BloccatoFinoUtc",
                table: "utenti");

            migrationBuilder.DropColumn(
                name: "TentativiLoginFalliti",
                table: "utenti");

            migrationBuilder.DropColumn(
                name: "TotpAttivatoAtUtc",
                table: "utenti");

            migrationBuilder.DropColumn(
                name: "TotpAttivo",
                table: "utenti");

            migrationBuilder.DropColumn(
                name: "TotpSecret",
                table: "utenti");
        }
    }
}
