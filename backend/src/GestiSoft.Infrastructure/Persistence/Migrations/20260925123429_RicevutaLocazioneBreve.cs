using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RicevutaLocazioneBreve : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModalitaPagamento",
                table: "dati_fattura",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CedolareSecca",
                table: "dati_aziendali",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IndirizzoImmobile",
                table: "dati_aziendali",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModalitaPagamento",
                table: "dati_fattura");

            migrationBuilder.DropColumn(
                name: "CedolareSecca",
                table: "dati_aziendali");

            migrationBuilder.DropColumn(
                name: "IndirizzoImmobile",
                table: "dati_aziendali");
        }
    }
}
