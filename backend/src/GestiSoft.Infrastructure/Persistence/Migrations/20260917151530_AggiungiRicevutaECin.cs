using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiRicevutaECin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_dati_fattura_StrutturaId_Anno_Progressivo",
                table: "dati_fattura");

            migrationBuilder.AddColumn<string>(
                name: "Cin",
                table: "impostazioni_struttura",
                type: "text",
                nullable: true);

            // defaultValue 1 = TipoEmissioneDocumento.Fattura, non 0 che non è nessun valore dell'enum:
            // tutto ciò che esiste oggi è una fattura e deve restare nella stessa serie di numerazione.
            // Con lo 0 generato da EF le fatture già emesse sarebbero finite in una serie a parte e il
            // progressivo delle nuove sarebbe ripartito da 1, duplicando numeri già usati.
            migrationBuilder.AddColumn<int>(
                name: "TipoEmissione",
                table: "dati_fattura",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_dati_fattura_StrutturaId_Anno_TipoEmissione_Progressivo",
                table: "dati_fattura",
                columns: new[] { "StrutturaId", "Anno", "TipoEmissione", "Progressivo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_dati_fattura_StrutturaId_Anno_TipoEmissione_Progressivo",
                table: "dati_fattura");

            migrationBuilder.DropColumn(
                name: "Cin",
                table: "impostazioni_struttura");

            migrationBuilder.DropColumn(
                name: "TipoEmissione",
                table: "dati_fattura");

            migrationBuilder.CreateIndex(
                name: "IX_dati_fattura_StrutturaId_Anno_Progressivo",
                table: "dati_fattura",
                columns: new[] { "StrutturaId", "Anno", "Progressivo" },
                unique: true);
        }
    }
}
