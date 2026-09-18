using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SpostaCinNeiDatiAziendali : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Prima si crea la colonna nuova e si travasa il valore, poi si elimina quella vecchia:
            // invertendo l'ordine - com'era stato generato - un CIN gia compilato andrebbe perso.
            migrationBuilder.AddColumn<string>(
                name: "Cin",
                table: "dati_aziendali",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                @"update dati_aziendali d
                  set ""Cin"" = i.""Cin""
                  from impostazioni_struttura i
                  where i.""StrutturaId"" = d.""StrutturaId"" and i.""Cin"" is not null;");

            migrationBuilder.DropColumn(
                name: "Cin",
                table: "impostazioni_struttura");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cin",
                table: "dati_aziendali");

            migrationBuilder.AddColumn<string>(
                name: "Cin",
                table: "impostazioni_struttura",
                type: "text",
                nullable: true);
        }
    }
}
