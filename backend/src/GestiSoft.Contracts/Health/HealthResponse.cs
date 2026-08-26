namespace GestiSoft.Contracts.Health;

public record HealthResponse(string Status, string Version, DateTime ServerTimeUtc);
