using GestiSoft.Application.Common;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Logging;

public class LogEventoService(ILogEventoRepository repository) : ILogEventoService
{
    // Politica di conservazione (principio di "limitazione della conservazione", GDPR art. 5.1.e:
    // i log non vanno tenuti oltre lo scopo per cui servono). Gli Info sono solo conferme che i job
    // girano regolarmente, servono a breve termine; Warning/Error restano più a lungo perché utili
    // per supporto e audit. Cancellazione netta, non anonimizzazione — scelta esplicita dell'utente.
    // Duplicato in frontend/src/pages/LogPage.tsx (nota informativa): tenere allineati.
    public const int GiorniConservazioneInfo = 180;
    public const int GiorniConservazioneAltri = 365;

    public async Task RegistraAsync(
        LivelloLog livello,
        string messaggio,
        string origine,
        string? dettaglio = null,
        string? correlationId = null,
        Guid? clienteId = null,
        Guid? strutturaId = null,
        string? categoria = null,
        string? operatore = null,
        CancellationToken cancellationToken = default)
    {
        var evento = new LogEvento
        {
            Livello = livello,
            Messaggio = messaggio,
            Origine = origine,
            Dettaglio = dettaglio,
            CorrelationId = correlationId,
            ClienteId = clienteId,
            StrutturaId = strutturaId,
            Categoria = categoria,
            Operatore = operatore,
        };

        await repository.AddAsync(evento, cancellationToken);
    }

    public async Task<PagedResult<LogEvento>> CercaAsync(LogEventoFiltro filtro, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await repository.SearchAsync(filtro, cancellationToken);
        return new PagedResult<LogEvento>(items, totalCount, filtro.Page, filtro.PageSize);
    }

    public Task<int> PulisciVecchiAsync(CancellationToken cancellationToken = default)
    {
        var ora = DateTime.UtcNow;
        return repository.EliminaPrecedentiAsync(
            ora.AddDays(-GiorniConservazioneInfo),
            ora.AddDays(-GiorniConservazioneAltri),
            cancellationToken);
    }
}
