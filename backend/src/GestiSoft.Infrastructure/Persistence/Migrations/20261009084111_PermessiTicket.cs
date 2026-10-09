using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PermessiTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TicketRead",
                table: "utenti_strutture",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TicketWrite",
                table: "utenti_strutture",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RichiedeTicket",
                table: "notifiche",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Nessun utente esistente deve perdere i ticket: fino a qui li apriva e leggeva chi
            // gestisce gli utenti della struttura.
            migrationBuilder.Sql(
                "UPDATE utenti_strutture SET \"TicketRead\" = \"SettingUser\", \"TicketWrite\" = \"SettingUser\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TicketRead",
                table: "utenti_strutture");

            migrationBuilder.DropColumn(
                name: "TicketWrite",
                table: "utenti_strutture");

            migrationBuilder.DropColumn(
                name: "RichiedeTicket",
                table: "notifiche");
        }
    }
}
