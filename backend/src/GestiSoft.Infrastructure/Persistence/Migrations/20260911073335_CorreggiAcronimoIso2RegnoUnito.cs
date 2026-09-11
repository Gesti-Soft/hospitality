using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Correzione di un dato, nessun cambio di schema: nella tabella di riferimento Stati il Regno
    /// Unito aveva Acronimo "GBF", che non è un codice ISO 3166-1 alpha-2 (l'errore arriva dai dati
    /// del sistema legacy, dove quel campo non veniva mai usato). Da qui l'Acronimo finisce nel campo
    /// Nazione/IdPaese della fattura elettronica, che accetta solo ISO2: va corretto in "GB".
    /// Il seed di riferimento popola le tabelle solo se vuote, quindi correggere il CSV embedded non
    /// basta per i database già popolati — serve questo UPDATE, mirato sulla riga col valore sbagliato.
    /// </summary>
    public partial class CorreggiAcronimoIso2RegnoUnito : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("UPDATE stati SET \"Acronimo\" = 'GB' WHERE \"Codice\" = 100000219 AND \"Acronimo\" = 'GBF';");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("UPDATE stati SET \"Acronimo\" = 'GBF' WHERE \"Codice\" = 100000219 AND \"Acronimo\" = 'GB';");
    }
}
