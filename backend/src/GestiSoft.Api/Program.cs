using System.Threading.RateLimiting;
using GestiSoft.Application;
using GestiSoft.Contracts.Health;
using GestiSoft.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Rate limiting per-tenant: evita che un singolo Cliente saturi il gestionale a danno degli
// altri, dato che ora è un'unica applicazione condivisa multi-tenant (non più un'installazione
// per cliente). Prima dell'autenticazione (Fase 2) la partizione è per IP; una volta introdotto
// il JWT, va ripartita per ClienteId preso dai claim, così il limite segue davvero il tenant e
// non l'indirizzo di rete (che può essere condiviso, es. NAT).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var partitionKey = httpContext.User.FindFirst("cliente_id")?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 200,
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 0,
        });
    });
});

var frontendOrigin = builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173";
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(frontendOrigin)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new HealthResponse("ok", "0.1.0", DateTime.UtcNow)))
    .WithName("Health")
    .WithTags("System");

app.Run();
