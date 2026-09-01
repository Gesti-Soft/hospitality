using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CambiaDefaultAnimaliAttivaFalse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "AnimaliAttiva",
                table: "prenotazioni",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            // Le prenotazioni esistenti erano state valorizzate a true solo dal vecchio default
            // della colonna (introdotta in questa stessa release), non da una scelta reale
            // dell'operatore — nessuna di loro ha davvero un animale, quindi si riallineano al
            // nuovo default corretto invece di restare "vere" per un mero accidente di migrazione.
            migrationBuilder.Sql("UPDATE prenotazioni SET \"AnimaliAttiva\" = false WHERE \"AnimaliAttiva\" = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "AnimaliAttiva",
                table: "prenotazioni",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }
    }
}
