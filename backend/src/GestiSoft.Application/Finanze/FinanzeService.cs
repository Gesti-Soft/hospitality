using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Finanze;

public record CreaSpesaRequest(string? TipoSpesa, string? Nome, decimal ImportoSpesa, string? Descrizione, string? MetodoPagamento, DateTime? DataSpesa);

public record CreaEntrataRequest(string? TipoEntrata, string? Nome, decimal ImportoEntrata, string? Descrizione, DateTime? Data);

public record RiepilogoCassaResult(int Anno, decimal ImportoPagatoPrenotazioni, decimal Cauzioni, decimal Entrate, decimal Spese, decimal Saldo, decimal CassaAttuale);

/// <summary>
/// Spese ed entrate di cassa — porta FinanzeLogic del legacy (CRUD puro, nessuna regola di
/// business oltre allo scoping per struttura, assente nel legacy single-tenant). Il riepilogo
/// cassa porta FinanzeLogic.GetCassa: nel legacy sommava su tutta la tabella senza filtro
/// struttura; qui è sempre filtrato per StrutturaId e per anno.
/// </summary>
public class FinanzeService(
    ISpesaRepository spese,
    IEntrataRepository entrate,
    ICauzioneRepository cauzioni,
    IPrenotazioneRepository prenotazioni,
    PermessoStrutturaGuard permessoGuard)
{
    public async Task<IReadOnlyList<Spesa>> ListaSpeseAsync(ICurrentUser currentUser, Guid strutturaId, int? anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        return await spese.ListAsync(strutturaId, anno, cancellationToken);
    }

    public async Task<Spesa> CreaSpesaAsync(ICurrentUser currentUser, Guid strutturaId, CreaSpesaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var entity = new Spesa
        {
            StrutturaId = strutturaId,
            TipoSpesa = request.TipoSpesa,
            Nome = request.Nome,
            ImportoSpesa = request.ImportoSpesa,
            Descrizione = request.Descrizione,
            MetodoPagamento = request.MetodoPagamento,
            DataSpesa = request.DataSpesa,
            Anno = (request.DataSpesa ?? DateTime.UtcNow).Year,
        };

        await spese.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<Spesa> AggiornaSpesaAsync(ICurrentUser currentUser, Guid strutturaId, Guid spesaId, CreaSpesaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var entity = await GetSpesaOwnedAsync(strutturaId, spesaId, cancellationToken);
        entity.TipoSpesa = request.TipoSpesa;
        entity.Nome = request.Nome;
        entity.ImportoSpesa = request.ImportoSpesa;
        entity.Descrizione = request.Descrizione;
        entity.MetodoPagamento = request.MetodoPagamento;
        entity.DataSpesa = request.DataSpesa;
        entity.Anno = (request.DataSpesa ?? DateTime.UtcNow).Year;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await spese.UpdateAsync(entity, cancellationToken);
        return entity;
    }

    public async Task EliminaSpesaAsync(ICurrentUser currentUser, Guid strutturaId, Guid spesaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var entity = await GetSpesaOwnedAsync(strutturaId, spesaId, cancellationToken);
        await spese.DeleteAsync(entity, cancellationToken);
    }

    public async Task<IReadOnlyList<Entrata>> ListaEntrateAsync(ICurrentUser currentUser, Guid strutturaId, int? anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        return await entrate.ListAsync(strutturaId, anno, cancellationToken);
    }

    public async Task<Entrata> CreaEntrataAsync(ICurrentUser currentUser, Guid strutturaId, CreaEntrataRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var entity = new Entrata
        {
            StrutturaId = strutturaId,
            TipoEntrata = request.TipoEntrata,
            Nome = request.Nome,
            ImportoEntrata = request.ImportoEntrata,
            Descrizione = request.Descrizione,
            Data = request.Data,
            Anno = (request.Data ?? DateTime.UtcNow).Year,
        };

        await entrate.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<Entrata> AggiornaEntrataAsync(ICurrentUser currentUser, Guid strutturaId, Guid entrataId, CreaEntrataRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var entity = await GetEntrataOwnedAsync(strutturaId, entrataId, cancellationToken);
        entity.TipoEntrata = request.TipoEntrata;
        entity.Nome = request.Nome;
        entity.ImportoEntrata = request.ImportoEntrata;
        entity.Descrizione = request.Descrizione;
        entity.Data = request.Data;
        entity.Anno = (request.Data ?? DateTime.UtcNow).Year;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await entrate.UpdateAsync(entity, cancellationToken);
        return entity;
    }

    public async Task EliminaEntrataAsync(ICurrentUser currentUser, Guid strutturaId, Guid entrataId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var entity = await GetEntrataOwnedAsync(strutturaId, entrataId, cancellationToken);
        await entrate.DeleteAsync(entity, cancellationToken);
    }

    public async Task<IReadOnlyList<Cauzione>> ListaCauzioniAsync(ICurrentUser currentUser, Guid strutturaId, int? anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        return await cauzioni.ListByStrutturaAsync(strutturaId, anno, cancellationToken);
    }

    /// <summary>Anni con almeno un dato di cassa (spesa, entrata, cauzione o incasso prenotazione) — per il selettore Anno condiviso da Riepilogo/Spese/Entrate/Cauzioni.</summary>
    public async Task<IReadOnlyList<int>> GetAnniDisponibiliAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);

        var anniSpese = await spese.ListaAnniConDatiAsync(strutturaId, cancellationToken);
        var anniEntrate = await entrate.ListaAnniConDatiAsync(strutturaId, cancellationToken);
        var anniCauzioni = await cauzioni.ListaAnniConDatiAsync(strutturaId, cancellationToken);
        var anniIncassi = await prenotazioni.ListaAnniConIncassoAsync(strutturaId, cancellationToken);

        return anniSpese.Concat(anniEntrate).Concat(anniCauzioni).Concat(anniIncassi)
            .Distinct()
            .OrderByDescending(a => a)
            .ToList();
    }

    /// <summary>
    /// Cassa = incassi prenotazioni non annullate + cauzioni trattenute + entrate − spese. `Saldo` è
    /// il netto del solo anno richiesto (utile per confrontare un anno con l'altro); `CassaAttuale` è
    /// invece cumulativo su tutta la storia della Struttura, senza filtro anno — quanto dovrebbe
    /// esserci realmente in cassa ad oggi, da quando la struttura ha iniziato a operare (porta
    /// FinanzeLogic.GetCassa del legacy, che sommava sempre l'intera tabella senza filtro anno).
    /// </summary>
    public async Task<RiepilogoCassaResult> RiepilogoCassaAsync(ICurrentUser currentUser, Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);

        var incassoPrenotazioni = await prenotazioni.SommaImportoPagatoAnnoAsync(strutturaId, anno, cancellationToken);
        var cauzioniAnno = await cauzioni.ListByStrutturaAsync(strutturaId, anno, cancellationToken);
        var totaleCauzioni = cauzioniAnno.Sum(c => c.ImportoCauzione ?? 0);
        var totaleEntrate = (await entrate.ListAsync(strutturaId, anno, cancellationToken)).Sum(e => e.ImportoEntrata);
        var totaleSpese = (await spese.ListAsync(strutturaId, anno, cancellationToken)).Sum(s => s.ImportoSpesa);
        var saldo = incassoPrenotazioni + totaleCauzioni + totaleEntrate - totaleSpese;

        var incassoTotale = await prenotazioni.SommaImportoPagatoTotaleAsync(strutturaId, cancellationToken);
        var cauzioniTotale = (await cauzioni.ListByStrutturaAsync(strutturaId, null, cancellationToken)).Sum(c => c.ImportoCauzione ?? 0);
        var entrateTotale = (await entrate.ListAsync(strutturaId, null, cancellationToken)).Sum(e => e.ImportoEntrata);
        var speseTotale = (await spese.ListAsync(strutturaId, null, cancellationToken)).Sum(s => s.ImportoSpesa);
        var cassaAttuale = incassoTotale + cauzioniTotale + entrateTotale - speseTotale;

        return new RiepilogoCassaResult(anno, incassoPrenotazioni, totaleCauzioni, totaleEntrate, totaleSpese, saldo, cassaAttuale);
    }

    private async Task<Spesa> GetSpesaOwnedAsync(Guid strutturaId, Guid spesaId, CancellationToken cancellationToken)
    {
        var entity = await spese.GetAsync(spesaId, cancellationToken) ?? throw new NotFoundException("Spesa non trovata.");
        if (entity.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Spesa non trovata.");
        }

        return entity;
    }

    private async Task<Entrata> GetEntrataOwnedAsync(Guid strutturaId, Guid entrataId, CancellationToken cancellationToken)
    {
        var entity = await entrate.GetAsync(entrataId, cancellationToken) ?? throw new NotFoundException("Entrata non trovata.");
        if (entity.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Entrata non trovata.");
        }

        return entity;
    }
}
