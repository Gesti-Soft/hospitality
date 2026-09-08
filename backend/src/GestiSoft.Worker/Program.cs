using GestiSoft.Application;
using GestiSoft.Infrastructure;
using GestiSoft.Worker.Jobs;
using Quartz;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Avvio GestiSoft.Worker");

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSerilog();

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApplication();

    // Scheduler in-process per i job di integrazione (sostituisce le code RabbitMQ del sistema
    // legacy). In Fase 1+ il job store passerà da RAM (default) a persistente su PostgreSQL,
    // così i job pianificati sopravvivono a un riavvio del Worker.
    builder.Services.AddQuartz(quartz =>
    {
        var heartbeatJobKey = new JobKey("heartbeat");
        quartz.AddJob<HeartbeatJob>(options => options.WithIdentity(heartbeatJobKey));
        quartz.AddTrigger(trigger => trigger
            .ForJob(heartbeatJobKey)
            .WithIdentity("heartbeat-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));

        // Fase 5 — Integrazione Wubook: hour timer per il pull prenotazioni, minute timer per gli
        // eventi via gestisoft.it. Il rinnovo periodico della licenza (ogni 2h) non esiste più: le
        // credenziali Wubook sono inserite a mano dal Super Admin, non più recuperate da
        // gestisoft.it (vedi WubookLicenzaService).
        var wubookPullJobKey = new JobKey("wubook-pull-prenotazioni");
        quartz.AddJob<WubookPullPrenotazioniJob>(options => options.WithIdentity(wubookPullJobKey));
        quartz.AddTrigger(trigger => trigger
            .ForJob(wubookPullJobKey)
            .WithIdentity("wubook-pull-prenotazioni-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInHours(1).RepeatForever()));

        var wubookEventiJobKey = new JobKey("wubook-eventi-polling");
        quartz.AddJob<WubookEventiPollingJob>(options => options.WithIdentity(wubookEventiJobKey));
        quartz.AddTrigger(trigger => trigger
            .ForJob(wubookEventiJobKey)
            .WithIdentity("wubook-eventi-polling-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));

        // Fase 6/7/8 — Alloggiati Web/Osservatorio/PayTourist ("schedine"): spostati in un processo
        // separato (GestiSoft.WorkerSchedine), condividono lo stesso Postgres ma girano in un
        // eseguibile a parte — un rallentamento/blocco su uno di questi sistemi esterni (Questura/
        // PMS, non controllati da noi) non deve rallentare il polling Wubook qui sopra, che oggi
        // condividerebbe altrimenti lo stesso processo/thread pool.

        // Notifiche in-app: scadenza licenza GestiSoft (non urgente, controllo orario è sufficiente)
        // e check-out dimenticato (idem — un ritardo di qualche minuto nel segnalarlo non cambia nulla).
        var licenzaScadenzaJobKey = new JobKey("licenza-scadenza-notifica");
        quartz.AddJob<LicenzaScadenzaNotificaJob>(options => options.WithIdentity(licenzaScadenzaJobKey));
        quartz.AddTrigger(trigger => trigger
            .ForJob(licenzaScadenzaJobKey)
            .WithIdentity("licenza-scadenza-notifica-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInHours(1).RepeatForever()));

        var checkOutDimenticatoJobKey = new JobKey("checkout-dimenticato-notifica");
        quartz.AddJob<CheckOutDimenticatoNotificaJob>(options => options.WithIdentity(checkOutDimenticatoJobKey));
        quartz.AddTrigger(trigger => trigger
            .ForJob(checkOutDimenticatoJobKey)
            .WithIdentity("checkout-dimenticato-notifica-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInHours(1).RepeatForever()));

        // Pulizia LogEvento (richiesta esplicita dell'utente, politica di conservazione GDPR): non
        // urgente, un giro al giorno basta.
        var puliziaLogJobKey = new JobKey("pulizia-log");
        quartz.AddJob<PuliziaLogJob>(options => options.WithIdentity(puliziaLogJobKey));
        quartz.AddTrigger(trigger => trigger
            .ForJob(puliziaLogJobKey)
            .WithIdentity("pulizia-log-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInHours(24).RepeatForever()));
    });
    builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "GestiSoft.Worker terminato in modo imprevisto");
}
finally
{
    Log.CloseAndFlush();
}
