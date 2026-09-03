using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiEtaEsenzioneTassaSoggiorno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TassaSoggiornoEtaEsenzioneAnziani",
                table: "impostazioni_struttura",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TassaSoggiornoEtaEsenzioneMinori",
                table: "impostazioni_struttura",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TassaSoggiornoEtaEsenzioneAnziani",
                table: "impostazioni_struttura");

            migrationBuilder.DropColumn(
                name: "TassaSoggiornoEtaEsenzioneMinori",
                table: "impostazioni_struttura");
        }
    }
}
