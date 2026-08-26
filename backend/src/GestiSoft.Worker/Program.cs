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
