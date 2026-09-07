using GestiSoft.Application;
using GestiSoft.Infrastructure;
using GestiSoft.WorkerSchedine.Jobs;
using Quartz;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Avvio GestiSoft.WorkerSchedine");

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSerilog();

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApplication();

    // Processo separato dal Worker principale (Wubook + notifiche generali), pur condividendo lo
    // stesso Postgres: solo i job "schedine" (Alloggiati Web/Osservatorio/PayTourist), che chiamano
    // sistemi esterni della Questura/PMS non controllati da noi — un rallentamento/blocco su uno di
    // questi non deve rallentare il polling Wubook, che ora gira nel suo processo indipendente.
    // Nessuna chiamata di rete tra i due processi: ognuno scrive/legge LogEvento/Notifica
    // direttamente sullo stesso database condiviso.
    builder.Services.AddQuartz(quartz =>
    {
        var alloggiatiWebJobKey = new JobKey("alloggiati-web-invio-giornaliero");
        quartz.AddJob<AlloggiatiWebInvioGiornalieroJob>(options => options.WithIdentity(alloggiatiWebJobKey));
        quartz.AddTrigger(trigger => trigger
            .ForJob(alloggiatiWebJobKey)
            .WithIdentity("alloggiati-web-invio-giornaliero-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));

        var osservatorioJobKey = new JobKey("osservatorio-invio-giornaliero");
        quartz.AddJob<OsservatorioInvioGiornalieroJob>(options => options.WithIdentity(osservatorioJobKey));
        quartz.AddTrigger(trigger => trigger
            .ForJob(osservatorioJobKey)
            .WithIdentity("osservatorio-invio-giornaliero-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));

        var payTouristJobKey = new JobKey("paytourist-invio-giornaliero");
        quartz.AddJob<PayTouristInvioGiornalieroJob>(options => options.WithIdentity(payTouristJobKey));
        quartz.AddTrigger(trigger => trigger
            .ForJob(payTouristJobKey)
            .WithIdentity("paytourist-invio-giornaliero-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));
    });
    builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "GestiSoft.WorkerSchedine terminato in modo imprevisto");
}
finally
{
    Log.CloseAndFlush();
}
