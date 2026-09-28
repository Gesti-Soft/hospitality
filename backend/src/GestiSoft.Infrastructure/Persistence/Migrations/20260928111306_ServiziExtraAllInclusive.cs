using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ServiziExtraAllInclusive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "servizi_struttura",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Prezzo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Modalita = table.Column<int>(type: "integer", nullable: false),
                    Attivo = table.Column<bool>(type: "boolean", nullable: false),
                    Eliminato = table.Column<bool>(type: "boolean", nullable: false),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servizi_struttura", x => x.Id);
                    table.ForeignKey(
                        name: "FK_servizi_struttura_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prenotazioni_servizi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrenotazioneId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServizioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Modalita = table.Column<int>(type: "integer", nullable: false),
                    PrezzoUnitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantita = table.Column<int>(type: "integer", nullable: false),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prenotazioni_servizi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prenotazioni_servizi_prenotazioni_PrenotazioneId",
                        column: x => x.PrenotazioneId,
                        principalTable: "prenotazioni",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_prenotazioni_servizi_servizi_struttura_ServizioId",
                        column: x => x.ServizioId,
                        principalTable: "servizi_struttura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prenotazioni_servizi_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prenotazioni_servizi_PrenotazioneId_ServizioId",
                table: "prenotazioni_servizi",
                columns: new[] { "PrenotazioneId", "ServizioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prenotazioni_servizi_ServizioId",
                table: "prenotazioni_servizi",
                column: "ServizioId");

            migrationBuilder.CreateIndex(
                name: "IX_prenotazioni_servizi_StrutturaId",
                table: "prenotazioni_servizi",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_servizi_struttura_StrutturaId",
                table: "servizi_struttura",
                column: "StrutturaId");

            // Ogni struttura ha i quattro trattamenti fin dalla creazione (TrattamentiService.BaseDellaStruttura):
            // a quelle esistenti si aggiungono solo quelli che mancano, non offerti e senza prezzo. I listini
            // già salvati restano come sono.
            migrationBuilder.Sql("""
                INSERT INTO trattamenti_struttura ("Id", "Tipo", "Attivo", "PrezzoPerPersona", "TipoPrezzoBambini", "StrutturaId", "CreatedAtUtc")
                SELECT gen_random_uuid(), t.tipo, false, 0, 1, s."Id", now()
                FROM strutture s
                CROSS JOIN (VALUES (1), (2), (3), (4)) AS t(tipo)
                WHERE NOT EXISTS (
                    SELECT 1 FROM trattamenti_struttura x WHERE x."StrutturaId" = s."Id" AND x."Tipo" = t.tipo);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prenotazioni_servizi");

            migrationBuilder.DropTable(
                name: "servizi_struttura");
        }
    }
}
