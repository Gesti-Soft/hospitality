using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiNascitaDatiCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataNascita",
                table: "dati_cliente",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LuogoNascita",
                table: "dati_cliente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Sesso",
                table: "dati_cliente",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataNascita",
                table: "dati_cliente");

            migrationBuilder.DropColumn(
                name: "LuogoNascita",
                table: "dati_cliente");

            migrationBuilder.DropColumn(
                name: "Sesso",
                table: "dati_cliente");
        }
    }
}
