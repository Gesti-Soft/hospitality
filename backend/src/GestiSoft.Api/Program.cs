using System.Text;
using GestiSoft.Api;
using System.Threading.RateLimiting;
using GestiSoft.Api.Auth;
using GestiSoft.Api.Middleware;
using GestiSoft.Application;
using GestiSoft.Application.Auth;
using GestiSoft.Contracts.Health;
using GestiSoft.Infrastructure;
using GestiSoft.Infrastructure.Auth;
using GestiSoft.Infrastructure.Persistence;
using GestiSoft.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using Serilog;

// Community license QuestPDF: gratuita per aziende sotto 1M$ di fatturato annuo — richiesta
// esplicitamente dalla libreria prima di generare qualsiasi documento.
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    // Rete di sicurezza: i controller devono sempre restituire DTO (mai entità EF direttamente,
    // per non rischiare di esporre campi sensibili come PasswordHash), ma se qualcuno se ne
    // dimenticasse in futuro, meglio omettere il ciclo che rispondere con un 500.
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Gestione errori globale: risposta ProblemDetails con messaggio sicuro + correlationId,
// dettaglio completo nei log (vedi GlobalExceptionHandler).
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Autenticazione JWT + ICurrentUser (letto dai claim per l'intera richiesta).
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? throw new InvalidOperationException("Sezione 'Jwt' non configurata.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Senza questo, ASP.NET Core rimappa i claim standard ("sub", "email") su URI lunghi
        // legacy per compatibilità storica, diversi da quelli letti in HttpContextCurrentUser.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// Rate limiting per-tenant: evita che un singolo Cliente saturi il gestionale a danno degli
// altri, dato che è un'unica applicazione condivisa multi-tenant (non un'installazione per
// cliente). Partizionato per cliente_id quando l'utente è autenticato, altrimenti per IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Il login è l'unico endpoint interrogabile senza essere già autenticati, quindi è l'unico
    // punto da cui si può provare a indovinare una password dall'esterno: qui il tetto è per
    // indirizzo IP e molto più stretto di quello generale. Il blocco dell'account dopo 5 tentativi
    // (AuthService) protegge il singolo utente; questo limita chi prova molte email diverse dallo
    // stesso posto, che il contatore per account non vedrebbe mai.
    options.AddPolicy(RateLimitPolicies.Login, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var partitionKey = httpContext.User.FindFirst(AppClaimTypes.ClienteId)?.Value
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

// Migrazioni EF applicate automaticamente all'avvio (idempotente — applica solo quelle mancanti,
// no-op se lo schema è già aggiornato): da qui in poi un aggiornamento è solo git pull + rebuild +
// restart, senza più un passo manuale `dotnet ef database update` a parte. Solo l'Api la esegue
// (non Worker/WorkerSchedine, per evitare corse concorrenti sulla stessa migrazione).
using (var migrationScope = app.Services.CreateScope())
{
    var dbContext = migrationScope.ServiceProvider.GetRequiredService<GestiSoftDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Popola i dati di base se assenti (idempotente, non richiede alcun file esterno):
// tabelle di riferimento Alloggiati Web + primo Super Admin (da configurazione, non hardcoded
// come "admin"/"admin" del legacy).
using (var startupScope = app.Services.CreateScope())
{
    var referenceDataSeeder = startupScope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>();
    await referenceDataSeeder.SeedAsync();

    var identitySeeder = startupScope.ServiceProvider.GetRequiredService<IdentitySeeder>();
    await identitySeeder.SeedAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new HealthResponse("ok", "0.1.0", DateTime.UtcNow)))
    .WithName("Health")
    .WithTags("System");

app.Run();
