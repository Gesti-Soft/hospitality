using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Utenti;
using GestiSoft.Contracts.Logging;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("logs")]
[Authorize]
public class LogController(ILogEventoService logEventoService, UtenteManagementService utenteManagement, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Cerca(
        [FromQuery] Guid? strutturaId,
        [FromQuery] LivelloLog? livello,
        [FromQuery] string? categoria = null,
        [FromQuery] string? ricerca = null,
        [FromQuery] DateTime? da = null,
        [FromQuery] DateTime? a = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        // La pagina Log è riservata a chi gestisce gli utenti della struttura (permesso
        // SettingUser) — un lavoratore normale non deve nemmeno poterla interrogare via Api, non
        // solo non vederla nel menu (richiesta esplicita). Senza una struttura selezionata non è
        // possibile stabilirlo, quindi è obbligatoria per chiunque non sia Super Admin.
        if (!currentUser.IsSuperAdmin)
        {
            if (strutturaId is not { } strutturaRichiesta)
            {
                throw new ForbiddenException("Seleziona una struttura per consultare il log.");
            }

            if (!await utenteManagement.HaGestioneUtentiAsync(currentUser, strutturaRichiesta, cancellationToken))
            {
                throw new ForbiddenException("Solo chi gestisce gli utenti di questa struttura può consultare il log.");
            }
        }

        // Un Cliente vede solo i propri log, a prescindere da cosa passa in query — solo il
        // Super Admin può vedere/filtrare su tutti i Clienti. Un Cliente (qui sempre un
        // amministratore della struttura, vedi controllo sopra) vede inoltre solo un sottoinsieme
        // delle categorie (vedi LogVisibilita), con "Auth" incluso: da amministratore deve poter
        // vedere i login di TUTTI i lavoratori della sua struttura, non solo il proprio — errori
        // generici, sincronizzazioni Wubook e azioni interne del Super Admin restano comunque
        // sempre riservati.
        var clienteId = currentUser.IsSuperAdmin ? null : currentUser.ClienteId;
        IReadOnlyList<string>? categorieVisibili = currentUser.IsSuperAdmin ? null : [.. LogVisibilita.CategorieVisibiliCliente, "Auth"];

        var filtro = new LogEventoFiltro(clienteId, strutturaId, livello, categoria, ricerca, da, a, page, pageSize, categorieVisibili);
        var risultato = await logEventoService.CercaAsync(filtro, cancellationToken);

        return Ok(new PagedResultDto<LogEventoDto>(risultato.Items.Select(ToDto).ToList(), risultato.TotalCount, risultato.Page, risultato.PageSize));
    }

    private static LogEventoDto ToDto(LogEvento e) => new(
        e.Id, e.ClienteId, e.StrutturaId, e.Livello, e.Messaggio, e.Dettaglio, e.CorrelationId, e.Origine, e.CreatedAtUtc, e.Categoria, e.Operatore);
}
