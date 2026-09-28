namespace GestiSoft.Domain.Enums;

/// <summary>
/// Da dove viene una riga di fattura: il soggiorno di una prenotazione, un servizio extra venduto con
/// lei, o una riga scritta a mano. Serve a non fatturare due volte la stessa cosa: il soggiorno di una
/// prenotazione e ogni suo servizio stanno in una fattura sola.
/// </summary>
public enum TipoRigaFattura
{
    Soggiorno = 1,
    Servizio = 2,
    Altro = 3,
}
