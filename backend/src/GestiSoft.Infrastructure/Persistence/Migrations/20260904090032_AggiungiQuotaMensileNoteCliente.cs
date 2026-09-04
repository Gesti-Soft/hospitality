using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiQuotaMensileNoteCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "clienti",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuotaMensile",
                table: "clienti",
                type: "numeric(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Note",
                table: "clienti");

            migrationBuilder.DropColumn(
                name: "QuotaMensile",
                table: "clienti");
        }
    }
}
