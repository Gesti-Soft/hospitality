using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiWubookDisponibilitaCamera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WubookDisponibilita",
                table: "camere",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WubookDisponibilita",
                table: "camere");
        }
    }
}
