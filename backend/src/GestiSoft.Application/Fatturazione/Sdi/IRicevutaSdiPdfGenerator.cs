namespace GestiSoft.Application.Fatturazione.Sdi;

/// <summary>
/// La copia leggibile in PDF di un messaggio dello SdI (consegna, scarto, mancata consegna…), fatta
/// dal file XML firmato così com'è arrivato. Il valore probatorio resta del file XML: il PDF lo dice.
/// </summary>
public interface IRicevutaSdiPdfGenerator
{
    /// <param name="prova">Arrivata dallo SdI di collaudo: il PDF lo scrive in evidenza.</param>
    byte[] GeneraPdf(byte[] xml, string? nomeFile, bool prova);
}
