using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PermessiCamereCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CheckInOut",
                table: "utenti_strutture",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RoomSetupRead",
                table: "utenti_strutture",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Nessun utente esistente deve vedere o perdere qualcosa: i due permessi nuovi vanno a chi
            // oggi arriva a quelle pagine. Camere e Tipologie chiedevano "Camere: consulta" insieme a
            // "Prenotazioni: consulta", e il pulsante del check-in stava in una pagina che chiedeva
            // "Prenotazioni: consulta" insieme a "Stato camera".
            migrationBuilder.Sql(
                "UPDATE utenti_strutture SET " +
                "\"RoomSetupRead\" = (\"SettingRoomRead\" AND \"ReservationRead\"), " +
                "\"CheckInOut\" = (\"RoomStatusUpdate\" AND \"ReservationRead\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckInOut",
                table: "utenti_strutture");

            migrationBuilder.DropColumn(
                name: "RoomSetupRead",
                table: "utenti_strutture");
        }
    }
}
