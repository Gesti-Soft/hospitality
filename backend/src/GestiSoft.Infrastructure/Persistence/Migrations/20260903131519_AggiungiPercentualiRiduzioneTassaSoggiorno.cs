using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiPercentualiRiduzioneTassaSoggiorno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TassaSoggiornoPercentualeAnziani",
                table: "impostazioni_struttura",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TassaSoggiornoPercentualeMinori",
                table: "impostazioni_struttura",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TassaSoggiornoPercentualeResidenti",
                table: "impostazioni_struttura",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TassaSoggiornoPercentualeAnziani",
                table: "impostazioni_struttura");

            migrationBuilder.DropColumn(
                name: "TassaSoggiornoPercentualeMinori",
                table: "impostazioni_struttura");

            migrationBuilder.DropColumn(
                name: "TassaSoggiornoPercentualeResidenti",
                table: "impostazioni_struttura");
        }
    }
}
