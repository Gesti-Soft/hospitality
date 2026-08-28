using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCodiceWoodooPrezzoOverrideCamera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodiceCameraWubook",
                table: "camere",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrezzoWubookOverride",
                table: "camere",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WubookSoloWoodoo",
                table: "camere",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodiceCameraWubook",
                table: "camere");

            migrationBuilder.DropColumn(
                name: "PrezzoWubookOverride",
                table: "camere");

            migrationBuilder.DropColumn(
                name: "WubookSoloWoodoo",
                table: "camere");
        }
    }
}
