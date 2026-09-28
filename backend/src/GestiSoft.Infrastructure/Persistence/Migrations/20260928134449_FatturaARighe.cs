using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FatturaARighe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AliquotaIva",
                table: "servizi_struttura",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Natura",
                table: "servizi_struttura",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fatture_prenotazioni",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatiFatturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrenotazioneId = table.Column<Guid>(type: "uuid", nullable: false),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fatture_prenotazioni", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fatture_prenotazioni_dati_fattura_DatiFatturaId",
                        column: x => x.DatiFatturaId,
                        principalTable: "dati_fattura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fatture_prenotazioni_prenotazioni_PrenotazioneId",
                        column: x => x.PrenotazioneId,
                        principalTable: "prenotazioni",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fatture_prenotazioni_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "righe_fattura",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatiFatturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Descrizione = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Quantita = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PrezzoUnitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PrezzoTotale = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AliquotaIva = table.Column<int>(type: "integer", nullable: true),
                    Natura = table.Column<int>(type: "integer", nullable: true),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    PrenotazioneId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrenotazioneServizioId = table.Column<Guid>(type: "uuid", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_righe_fattura", x => x.Id);
                    table.ForeignKey(
                        name: "FK_righe_fattura_dati_fattura_DatiFatturaId",
                        column: x => x.DatiFatturaId,
                        principalTable: "dati_fattura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_righe_fattura_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fatture_prenotazioni_DatiFatturaId_PrenotazioneId",
                table: "fatture_prenotazioni",
                columns: new[] { "DatiFatturaId", "PrenotazioneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fatture_prenotazioni_PrenotazioneId",
                table: "fatture_prenotazioni",
                column: "PrenotazioneId");

            migrationBuilder.CreateIndex(
                name: "IX_fatture_prenotazioni_StrutturaId",
                table: "fatture_prenotazioni",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_righe_fattura_DatiFatturaId",
                table: "righe_fattura",
                column: "DatiFatturaId");

            migrationBuilder.CreateIndex(
                name: "IX_righe_fattura_PrenotazioneId",
                table: "righe_fattura",
                column: "PrenotazioneId");

            migrationBuilder.CreateIndex(
                name: "IX_righe_fattura_PrenotazioneServizioId",
                table: "righe_fattura",
                column: "PrenotazioneServizioId");

            migrationBuilder.CreateIndex(
                name: "IX_righe_fattura_StrutturaId",
                table: "righe_fattura",
                column: "StrutturaId");

            // Prima di togliere le colonne della vecchia riga unica, la si copia come riga 1 di ogni
            // documento. Tipo 1 = Soggiorno quando la fattura era legata a una prenotazione (così il
            // soggiorno risulta già fatturato e non si propone una seconda volta), 3 = Altro se no.
            // Stesso ordine per il legame con la prenotazione.
            migrationBuilder.Sql("""
                INSERT INTO righe_fattura ("Id", "DatiFatturaId", "Numero", "Descrizione", "Quantita", "PrezzoUnitario", "PrezzoTotale", "AliquotaIva", "Natura", "Tipo", "PrenotazioneId", "PrenotazioneServizioId", "StrutturaId", "CreatedAtUtc")
                SELECT gen_random_uuid(), f."Id", 1, COALESCE(f."Descrizione", ''), f."Quantita", f."PrezzoUnitario", f."PrezzoTotale", f."AliquotaIva", f."Natura",
                       CASE WHEN f."PrenotazioneId" IS NULL THEN 3 ELSE 1 END, f."PrenotazioneId", NULL, f."StrutturaId", now()
                FROM dati_fattura f;

                INSERT INTO fatture_prenotazioni ("Id", "DatiFatturaId", "PrenotazioneId", "StrutturaId", "CreatedAtUtc")
                SELECT gen_random_uuid(), f."Id", f."PrenotazioneId", f."StrutturaId", now()
                FROM dati_fattura f
                WHERE f."PrenotazioneId" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "AliquotaIva",
                table: "dati_fattura");

            migrationBuilder.DropColumn(
                name: "Descrizione",
                table: "dati_fattura");

            migrationBuilder.DropColumn(
                name: "Natura",
                table: "dati_fattura");

            migrationBuilder.DropColumn(
                name: "PrezzoUnitario",
                table: "dati_fattura");

            migrationBuilder.DropColumn(
                name: "Quantita",
                table: "dati_fattura");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fatture_prenotazioni");

            migrationBuilder.DropTable(
                name: "righe_fattura");

            migrationBuilder.DropColumn(
                name: "AliquotaIva",
                table: "servizi_struttura");

            migrationBuilder.DropColumn(
                name: "Natura",
                table: "servizi_struttura");

            migrationBuilder.AddColumn<int>(
                name: "AliquotaIva",
                table: "dati_fattura",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Descrizione",
                table: "dati_fattura",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Natura",
                table: "dati_fattura",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrezzoUnitario",
                table: "dati_fattura",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantita",
                table: "dati_fattura",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
