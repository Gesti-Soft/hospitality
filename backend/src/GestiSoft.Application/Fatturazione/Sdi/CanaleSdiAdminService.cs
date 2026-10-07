using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Fatturazione.Sdi;

/// <summary>Lo stato del canale e quello che aspetta il Super Admin.</summary>
public record StatoCanaleSdi(
    string? CodiceDestinatario,
    string? CodiceFiscaleTrasmittente,
    bool Collaudo,
    IReadOnlyList<FileRicevutoSdi> Quarantena,
    IReadOnlyList<RicevutaSdi> RicevuteNonAbbinate,
    IReadOnlyList<FileRicevutoSdi> RicezioniDiCollaudo);

/// <summary>
/// Pannello "Canale SdI" del Super Admin: il canale è uno per tutti i Clienti, quindi le fatture
/// ricevute che non si sa a chi dare e le ricevute orfane le vede solo lui. Attribuire una fattura in
/// quarantena la mostra a un Cliente: è un'azione che resta nel Log, con chi l'ha fatta.
/// </summary>
public class CanaleSdiAdminService(
    ICanaleSdiAdminRepository repository,
    IConfigurazioneCanaleSdi canale,
    SdiRicezioneService ricezione,
    IStrutturaRepository strutture,
    ILogEventoService logEventi)
{
    private const int MassimoElenco = 100;

    public async Task<StatoCanaleSdi> StatoAsync(ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        return new StatoCanaleSdi(
            canale.CodiceDestinatario,
            canale.CodiceFiscaleTrasmittente,
            canale.Collaudo,
            await repository.FileInQuarantenaAsync(cancellationToken),
            await repository.RicevuteNonAbbinateAsync(MassimoElenco, cancellationToken),
            await repository.FileDiCollaudoAsync(MassimoElenco, cancellationToken));
    }

    /// <summary>
    /// Dà a una struttura una fattura in quarantena. Non si ricontrolla il destinatario: è proprio
    /// quello che è fallito. La vedranno tutte le strutture del suo Cliente, come ogni fattura ricevuta.
    /// </summary>
    public async Task AttribuisciAsync(ICurrentUser currentUser, Guid fileRicevutoId, Guid strutturaId, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        var file = await repository.FileRicevutoAsync(fileRicevutoId, cancellationToken) ?? throw new NotFoundException("File ricevuto non trovato.");
        if (file.Prova || file.StrutturaId is not null || file.Esito is not (EsitoFileRicevutoSdi.SenzaDestinatario or EsitoFileRicevutoSdi.PiuDestinatari))
        {
            throw new ConflictException("Questo file non è in attesa di essere attribuito.");
        }

        var xml = LettoreXmlSdi.EstraiXmlFattura(file.File);
        var letta = (xml is null ? null : LettoreXmlSdi.LeggiFattura(xml))
            ?? throw new ConflictException("Il file non contiene una fattura leggibile: si può solo scaricare.");

        var struttura = await strutture.GetByIdAsync(strutturaId, cancellationToken) ?? throw new NotFoundException("Struttura non trovata.");
        var delCliente = await strutture.ListByClienteAsync(struttura.ClienteId, includiInattive: true, cancellationToken);
        // La prima è quella scelta: la fattura si appoggia lì. Le altre servono a non registrarla due volte.
        var strutturaIds = new[] { struttura.Id }.Concat(delCliente.Select(s => s.Id).Where(id => id != struttura.Id)).ToList();

        await ricezione.AttribuisciAsync(file, letta, strutturaIds, cancellationToken);
        await logEventi.RegistraAsync(
            LivelloLog.Info,
            $"Fattura ricevuta dallo SdI (identificativo {file.IdentificativoSdi}) attribuita a mano dalla quarantena.",
            origine: "SuperAdmin",
            clienteId: struttura.ClienteId,
            strutturaId: struttura.Id,
            categoria: "Fatturazione",
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);
    }

    public async Task<(string NomeFile, byte[] File)> FileRicevutoAsync(ICurrentUser currentUser, Guid fileRicevutoId, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        var file = await repository.FileRicevutoAsync(fileRicevutoId, cancellationToken) ?? throw new NotFoundException("File ricevuto non trovato.");
        return (file.NomeFile, file.File);
    }

    public async Task<(string NomeFile, byte[] File)> RicevutaAsync(ICurrentUser currentUser, Guid ricevutaId, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        var ricevuta = await repository.RicevutaAsync(ricevutaId, cancellationToken) ?? throw new NotFoundException("Ricevuta non trovata.");
        return (ricevuta.NomeFile ?? $"ricevuta-{ricevuta.IdentificativoSdi ?? ricevuta.Id.ToString()}.xml", ricevuta.Xml);
    }

    private static void RichiediSuperAdmin(ICurrentUser currentUser)
    {
        if (!currentUser.IsSuperAdmin)
        {
            throw new ForbiddenException("Solo il Super Admin può accedere al canale SdI.");
        }
    }
}
