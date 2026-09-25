using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RiduzioneOspiteInMeno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RiduzioneOspiteInMeno",
                table: "tipologie_camera",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoRiduzioneOspiteInMeno",
                table: "tipologie_camera",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RiduzioneOspiteInMeno",
                table: "tipologie_camera");

            migrationBuilder.DropColumn(
                name: "TipoRiduzioneOspiteInMeno",
                table: "tipologie_camera");
        }
    }
}
