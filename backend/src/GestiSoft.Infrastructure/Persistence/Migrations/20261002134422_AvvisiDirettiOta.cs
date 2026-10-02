using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AvvisiDirettiOta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AvvisiDiretti",
                table: "wubook_integrazioni",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UrlAvvisiPrecedente",
                table: "wubook_integrazioni",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DaElaborare",
                table: "wubook_eventi_ricevuti",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProssimoTentativoUtc",
                table: "wubook_eventi_ricevuti",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tentativi",
                table: "wubook_eventi_ricevuti",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvvisiDiretti",
                table: "wubook_integrazioni");

            migrationBuilder.DropColumn(
                name: "UrlAvvisiPrecedente",
                table: "wubook_integrazioni");

            migrationBuilder.DropColumn(
                name: "DaElaborare",
                table: "wubook_eventi_ricevuti");

            migrationBuilder.DropColumn(
                name: "ProssimoTentativoUtc",
                table: "wubook_eventi_ricevuti");

            migrationBuilder.DropColumn(
                name: "Tentativi",
                table: "wubook_eventi_ricevuti");
        }
    }
}
