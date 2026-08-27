namespace GestiSoft.Contracts.Finanze;

public record RiepilogoCassaDto(int Anno, decimal ImportoPagatoPrenotazioni, decimal Cauzioni, decimal Entrate, decimal Spese, decimal Saldo);
