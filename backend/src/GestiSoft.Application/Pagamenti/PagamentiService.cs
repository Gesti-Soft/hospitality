using System.Globalization;
using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Application.Pulizie;
using GestiSoft.Contracts.Pagamenti;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Pagamenti;

/// <summary>
/// Registro dei pagamenti di una prenotazione: acconto, caparra, saldo, rimborso, ognuno con data e
/// metodo. Prenotazione.ImportoPagato resta la somma (incassi meno rimborsi), ricalcolata a ogni
/// modifica: statistiche e schermate che lo leggono continuano a funzionare. Stessi permessi della
/// prenotazione, come prima quando l'importo pagato si scriveva a mano; ogni operazione va nel log.
/// </summary>
public class PagamentiService(
    IPagamentoPrenotazioneRepository pagamenti,
    IPrenotazioneRepository prenotazioni,
    PermessoStrutturaGuard permessoGuard,
    ILogEventoService logEventi)
{
    public const int LunghezzaMassimaNota = 200;

    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");

    public async Task<IReadOnlyList<PagamentoPrenotazione>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationRead, cancellationToken);
        await GetPrenotazioneAsync(strutturaId, prenotazioneId, cancellationToken);
        return await pagamenti.ListByPrenotazioneAsync(strutturaId, prenotazioneId, cancellationToken);
    }

    public async Task<PagamentoPrenotazione> RegistraAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, SalvaPagamentoRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationWrite, cancellationToken);
        var prenotazione = await GetPrenotazioneAsync(strutturaId, prenotazioneId, cancellationToken);
        Valida(request, PulizieSoggiornoService.Oggi());

        var pagamento = Nuovo(strutturaId, prenotazioneId, request, currentUser.Email);
        var esistenti = await pagamenti.ListByPrenotazioneAsync(strutturaId, prenotazioneId, cancellationToken);
        EnsureNonNegativo([.. esistenti, pagamento]);

        await pagamenti.AddAsync(pagamento, cancellationToken);
        await RicalcolaPagatoAsync(prenotazione, cancellationToken);
        await LogAsync(currentUser, prenotazione, $"registrato: {Descrivi(pagamento)}", cancellationToken);
        return pagamento;
    }

    public async Task<PagamentoPrenotazione> AggiornaAsync(
        ICurrentUser currentUser,
        Guid strutturaId,
        Guid prenotazioneId,
        Guid pagamentoId,
        SalvaPagamentoRequest request,
        CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationWrite, cancellationToken);
        var prenotazione = await GetPrenotazioneAsync(strutturaId, prenotazioneId, cancellationToken);
        var pagamento = await GetPagamentoAsync(strutturaId, prenotazioneId, pagamentoId, cancellationToken);
        // Un importo del vecchio "Importo pagato" non ha il metodo: correggendolo si può lasciarlo com'è.
        Valida(request, PulizieSoggiornoService.Oggi(), metodoFacoltativo: pagamento.Metodo is null);

        var prima = Descrivi(pagamento);
        pagamento.Data = request.Data;
        pagamento.Importo = request.Importo;
        pagamento.Tipo = request.Tipo;
        pagamento.Metodo = request.Metodo;
        pagamento.Nota = PulisciNota(request.Nota);
        pagamento.UpdatedAtUtc = DateTime.UtcNow;

        var esistenti = await pagamenti.ListByPrenotazioneAsync(strutturaId, prenotazioneId, cancellationToken);
        EnsureNonNegativo([.. esistenti.Where(p => p.Id != pagamento.Id), pagamento]);

        await pagamenti.UpdateAsync(pagamento, cancellationToken);
        await RicalcolaPagatoAsync(prenotazione, cancellationToken);
        await LogAsync(currentUser, prenotazione, $"corretto: {prima} → {Descrivi(pagamento)}", cancellationToken);
        return pagamento;
    }

    public async Task EliminaAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, Guid pagamentoId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationWrite, cancellationToken);
        var prenotazione = await GetPrenotazioneAsync(strutturaId, prenotazioneId, cancellationToken);
        var pagamento = await GetPagamentoAsync(strutturaId, prenotazioneId, pagamentoId, cancellationToken);

        var esistenti = await pagamenti.ListByPrenotazioneAsync(strutturaId, prenotazioneId, cancellationToken);
        EnsureNonNegativo(esistenti.Where(p => p.Id != pagamento.Id).ToList());

        await pagamenti.DeleteAsync(pagamento, cancellationToken);
        await RicalcolaPagatoAsync(prenotazione, cancellationToken);
        await LogAsync(currentUser, prenotazione, $"eliminato: {Descrivi(pagamento)}", cancellationToken);
    }

    /// <summary>
    /// Pagamenti di una prenotazione appena creata (l'acconto preso al telefono). Si controllano con
    /// <see cref="ValidaPerNuova"/> prima di creare la prenotazione, così un errore non la lascia a metà.
    /// </summary>
    public async Task RegistraPerNuovaAsync(ICurrentUser currentUser, Prenotazione prenotazione, IReadOnlyList<SalvaPagamentoRequest> richieste, CancellationToken cancellationToken)
    {
        foreach (var richiesta in richieste)
        {
            var pagamento = Nuovo(prenotazione.StrutturaId, prenotazione.Id, richiesta, currentUser.Email);
            await pagamenti.AddAsync(pagamento, cancellationToken);
            await LogAsync(currentUser, prenotazione, $"registrato: {Descrivi(pagamento)}", cancellationToken);
        }

        await RicalcolaPagatoAsync(prenotazione, cancellationToken);
    }

    public static void ValidaPerNuova(IReadOnlyList<SalvaPagamentoRequest> richieste)
    {
        var oggi = PulizieSoggiornoService.Oggi();
        foreach (var richiesta in richieste)
        {
            Valida(richiesta, oggi);
        }

        EnsureNonNegativo(richieste.Select(r => new PagamentoPrenotazione { Importo = r.Importo, Tipo = r.Tipo }).ToList());
    }

    /// <summary>Incassi meno rimborsi.</summary>
    public static decimal Netto(IEnumerable<PagamentoPrenotazione> righe) =>
        righe.Sum(p => p.Tipo == TipoPagamento.Rimborso ? -p.Importo : p.Importo);

    public static void Valida(SalvaPagamentoRequest request, DateTime oggi, bool metodoFacoltativo = false)
    {
        if (!Enum.IsDefined(request.Tipo))
        {
            throw new ConflictException("Tipo di pagamento non riconosciuto.");
        }

        if (request.Metodo is { } metodo ? !Enum.IsDefined(metodo) : !metodoFacoltativo)
        {
            throw new ConflictException("Indica come è stato pagato.");
        }

        if (request.Importo <= 0 || decimal.Round(request.Importo, 2) != request.Importo)
        {
            throw new ConflictException("L'importo deve essere maggiore di zero, con al massimo due decimali.");
        }

        // Si registra quello che è entrato davvero: un pagamento di domani non c'è ancora.
        if (request.Data > DateOnly.FromDateTime(oggi))
        {
            throw new ConflictException("La data del pagamento non può essere nel futuro.");
        }

        if (request.Nota?.Trim().Length > LunghezzaMassimaNota)
        {
            throw new ConflictException($"La nota può avere al massimo {LunghezzaMassimaNota} caratteri.");
        }
    }

    private static void EnsureNonNegativo(IReadOnlyList<PagamentoPrenotazione> righe)
    {
        if (Netto(righe) < 0)
        {
            throw new ConflictException("Il rimborso supera quanto è stato pagato.");
        }
    }

    private static PagamentoPrenotazione Nuovo(Guid strutturaId, Guid prenotazioneId, SalvaPagamentoRequest request, string? registratoDa) => new()
    {
        StrutturaId = strutturaId,
        PrenotazioneId = prenotazioneId,
        Data = request.Data,
        Importo = request.Importo,
        Tipo = request.Tipo,
        Metodo = request.Metodo,
        Nota = PulisciNota(request.Nota),
        RegistratoDa = registratoDa,
    };

    private static string? PulisciNota(string? nota) => string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();

    private async Task RicalcolaPagatoAsync(Prenotazione prenotazione, CancellationToken cancellationToken)
    {
        var righe = await pagamenti.ListByPrenotazioneAsync(prenotazione.StrutturaId, prenotazione.Id, cancellationToken);
        prenotazione.ImportoPagato = Netto(righe);
        prenotazione.UpdatedAtUtc = DateTime.UtcNow;
        await prenotazioni.UpdateAsync(prenotazione, cancellationToken);
    }

    private async Task<Prenotazione> GetPrenotazioneAsync(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        var prenotazione = await prenotazioni.GetAsync(prenotazioneId, cancellationToken);
        return prenotazione is not null && prenotazione.StrutturaId == strutturaId
            ? prenotazione
            : throw new NotFoundException("Prenotazione non trovata.");
    }

    private async Task<PagamentoPrenotazione> GetPagamentoAsync(Guid strutturaId, Guid prenotazioneId, Guid pagamentoId, CancellationToken cancellationToken)
    {
        var pagamento = await pagamenti.GetAsync(strutturaId, pagamentoId, cancellationToken);
        return pagamento is not null && pagamento.PrenotazioneId == prenotazioneId
            ? pagamento
            : throw new NotFoundException("Pagamento non trovato.");
    }

    /// <summary>Nel log importi, tipo, metodo e data: mai la nota, che è testo libero.</summary>
    private static string Descrivi(PagamentoPrenotazione p) =>
        $"{NomeTipo(p.Tipo)} {p.Importo.ToString("N2", Italiano)} €{(p.Metodo is { } m ? $" ({NomeMetodo(m)})" : "")} del {p.Data:dd/MM/yyyy}";

    public static string NomeTipo(TipoPagamento tipo) => tipo switch
    {
        TipoPagamento.Acconto => "acconto",
        TipoPagamento.Caparra => "caparra",
        TipoPagamento.Saldo => "saldo",
        TipoPagamento.Rimborso => "rimborso",
        _ => "pagamento",
    };

    private static string NomeMetodo(ModalitaPagamento metodo) => metodo switch
    {
        ModalitaPagamento.Contanti => "contanti",
        ModalitaPagamento.Bonifico => "bonifico",
        ModalitaPagamento.CartaDiPagamento => "carta",
        ModalitaPagamento.Assegno => "assegno",
        ModalitaPagamento.PortaleOnline => "portale online",
        _ => metodo.ToString(),
    };

    private async Task LogAsync(ICurrentUser currentUser, Prenotazione prenotazione, string azione, CancellationToken cancellationToken) =>
        await logEventi.RegistraAsync(
            LivelloLog.Info,
            $"Pagamento {azione} sulla prenotazione #{prenotazione.NumeroPrenotazione ?? prenotazione.Id.ToString()[..8]}.",
            origine: "Api",
            clienteId: currentUser.ClienteId,
            strutturaId: prenotazione.StrutturaId,
            categoria: "Prenotazione",
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);
}
