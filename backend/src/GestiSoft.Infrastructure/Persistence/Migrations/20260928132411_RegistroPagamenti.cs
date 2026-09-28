using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegistroPagamenti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pagamenti_prenotazione",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrenotazioneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Importo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Metodo = table.Column<int>(type: "integer", nullable: true),
                    Nota = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RegistratoDa = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagamenti_prenotazione", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pagamenti_prenotazione_prenotazioni_PrenotazioneId",
                        column: x => x.PrenotazioneId,
                        principalTable: "prenotazioni",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pagamenti_prenotazione_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pagamenti_prenotazione_PrenotazioneId",
                table: "pagamenti_prenotazione",
                column: "PrenotazioneId");

            migrationBuilder.CreateIndex(
                name: "IX_pagamenti_prenotazione_StrutturaId",
                table: "pagamenti_prenotazione",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_pagamenti_prenotazione_StrutturaId_Data",
                table: "pagamenti_prenotazione",
                columns: new[] { "StrutturaId", "Data" });

            // Il vecchio "Importo pagato", scritto a mano, diventa un pagamento del registro. La data non
            // si conosce: si usa quella del check-in, nello stesso anno in cui la Cassa lo contava già
            // (Anno della prenotazione), così i totali degli anni passati non cambiano. Tipo 4 = Altro,
            // metodo sconosciuto. Le annullate erano già escluse dalla Cassa e hanno comunque 0.
            migrationBuilder.Sql("""
                INSERT INTO pagamenti_prenotazione ("Id", "PrenotazioneId", "Data", "Importo", "Tipo", "Metodo", "Nota", "RegistratoDa", "StrutturaId", "CreatedAtUtc")
                SELECT gen_random_uuid(), p."Id",
                       CASE WHEN p."CheckIn" IS NOT NULL AND EXTRACT(YEAR FROM (p."CheckIn" AT TIME ZONE 'UTC')) = p."Anno"
                            THEN (p."CheckIn" AT TIME ZONE 'UTC')::date
                            ELSE make_date(p."Anno", 1, 1) END,
                       p."ImportoPagato", 4, NULL,
                       'Importo pagato registrato prima del registro dei pagamenti', NULL, p."StrutturaId", now()
                FROM prenotazioni p
                WHERE p."ImportoPagato" > 0 AND p."StatoPrenotazione" IS DISTINCT FROM 3;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pagamenti_prenotazione");
        }
    }
}
