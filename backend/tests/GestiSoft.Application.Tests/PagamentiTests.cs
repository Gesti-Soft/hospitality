using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Pagamenti;
using GestiSoft.Contracts.Pagamenti;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Registro pagamenti: il pagato è incassi meno rimborsi, un rimborso non supera il pagato, niente
/// pagamenti nel futuro né importi a zero, il metodo è obbligatorio tranne che per i vecchi importi.
/// </summary>
public class PagamentiTests
{
    private static readonly DateTime Oggi = new(2026, 10, 12);

    private static SalvaPagamentoRequest Richiesta(
        decimal importo = 100m,
        TipoPagamento tipo = TipoPagamento.Acconto,
        ModalitaPagamento? metodo = ModalitaPagamento.Contanti,
        DateOnly? data = null) => new(data ?? new DateOnly(2026, 10, 12), importo, tipo, metodo, null);

    [Fact]
    public void Netto_IncassiMenoRimborsi()
    {
        var righe = new List<PagamentoPrenotazione>
        {
            new() { Importo = 100m, Tipo = TipoPagamento.Caparra },
            new() { Importo = 250m, Tipo = TipoPagamento.Saldo },
            new() { Importo = 30m, Tipo = TipoPagamento.Rimborso },
        };

        Assert.Equal(320m, PagamentiService.Netto(righe));
    }

    [Fact]
    public void RimborsoOltreIlPagato_Rifiutato()
    {
        Assert.Throws<ConflictException>(() => PagamentiService.ValidaPerNuova([Richiesta(50m), Richiesta(80m, TipoPagamento.Rimborso)]));
    }

    [Fact]
    public void PagamentoNelFuturo_Rifiutato()
    {
        Assert.Throws<ConflictException>(() => PagamentiService.Valida(Richiesta(data: new DateOnly(2026, 10, 13)), Oggi));
    }

    [Fact]
    public void PagamentoDiOggi_Ammesso()
    {
        PagamentiService.Valida(Richiesta(), Oggi);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(10.005)]
    public void ImportoNonValido_Rifiutato(decimal importo)
    {
        Assert.Throws<ConflictException>(() => PagamentiService.Valida(Richiesta(importo), Oggi));
    }

    [Fact]
    public void SenzaMetodo_Rifiutato()
    {
        Assert.Throws<ConflictException>(() => PagamentiService.Valida(Richiesta(metodo: null), Oggi));
    }

    [Fact]
    public void SenzaMetodo_AmmessoPerIVecchiImporti()
    {
        PagamentiService.Valida(Richiesta(metodo: null), Oggi, metodoFacoltativo: true);
    }
}
