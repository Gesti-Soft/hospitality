using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Camere;

public interface IPrezzoCameraRepository
{
    Task<GestionePrezzo?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<GestionePrezzo>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>
    /// Periodi esistenti che si sovrappongono al nuovo, per lo stesso CameraId (se
    /// <paramref name="cameraId"/> valorizzato) o per la stessa Tipologia a livello di
    /// tipologia (se <paramref name="tipologiaId"/> valorizzato) — usata dall'algoritmo di
    /// split di AddOrUpdatePrice.
    /// </summary>
    Task<IReadOnlyList<GestionePrezzo>> ListSovrappostiAsync(
        Guid strutturaId,
        Guid? cameraId,
        Guid? tipologiaId,
        DateTime dataInizio,
        DateTime dataFine,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tutti i periodi pertinenti per il calcolo prezzo di una camera in un range di date: sia
    /// specifici della camera, sia di tipologia (CameraId nullo, TipologiaId della camera) —
    /// usata da GetPriceByRoom/GetImport per risolvere il prezzo giorno per giorno.
    /// </summary>
    Task<IReadOnlyList<GestionePrezzo>> ListPerCalendarioAsync(
        Guid strutturaId,
        Guid cameraId,
        Guid? tipologiaId,
        DateTime dataInizio,
        DateTime dataFine,
        CancellationToken cancellationToken);

    void Add(GestionePrezzo entity);

    void Remove(GestionePrezzo entity);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
