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

        // Fase 6 — Integrazione Alloggiati Web: porta lo StartDailyTaskTimer del legacy (controllo
        // ogni minuto se è stata raggiunta l'ora di invio configurata per struttura).
        var alloggiatiWebJobKey = new JobKey("alloggiati-web-invio-giornaliero");
        quartz.AddJob<AlloggiatiWebInvioGiornalieroJob>(options => options.WithIdentity(alloggiatiWebJobKey));
        quartz.AddTrigger(trigger => trigger
            .ForJob(alloggiatiWebJobKey)
            .WithIdentity("alloggiati-web-invio-giornaliero-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));

        // Fase 7 — Integrazione Osservatorio Turistico: stesso orario configurato di Alloggiati Web,
        // stesso controllo ogni minuto.
        var osservatorioJobKey = new JobKey("osservatorio-invio-giornaliero");
        quartz.AddJob<OsservatorioInvioGiornalieroJob>(options => options.WithIdentity(osservatorioJobKey));
        quartz.AddTrigger(trigger => trigger
            .ForJob(osservatorioJobKey)
            .WithIdentity("osservatorio-invio-giornaliero-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));

        // Fase 8 — Integrazione PayTourist: stesso orario condiviso di Alloggiati Web/Osservatorio,
        // stesso controllo ogni minuto.
        var payTouristJobKey = new JobKey("paytourist-invio-giornaliero");
        quartz.AddJob<PayTouristInvioGiornalieroJob>(options => options.WithIdentity(payTouristJobKey));
        quartz.AddTrigger(trigger => trigger
            .ForJob(payTouristJobKey)
            .WithIdentity("paytourist-invio-giornaliero-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));

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
