using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultIvaDatiAziendali : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AliquotaIvaDefault",
                table: "dati_aziendali",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NaturaDefault",
                table: "dati_aziendali",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AliquotaIvaDefault",
                table: "dati_aziendali");

            migrationBuilder.DropColumn(
                name: "NaturaDefault",
                table: "dati_aziendali");
        }
    }
}
