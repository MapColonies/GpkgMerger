using MergerLogic.Extensions;
using MergerLogic.Clients;
using MergerService.Src;
using MergerService.Utils;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.RegisterMergerLogicType();
builder.Services.AddSingleton<IRun, Run>();
builder.Services.AddSingleton<ITaskUtils, TaskUtils>();
builder.Services.AddSingleton<IHeartbeatClient, HeartbeatClient>();

var app = builder.Build();
app.MapControllers();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

AppDomain.CurrentDomain.UnhandledException += OnUnhandledExceptionEventHandler;

new Thread(() =>
{
    try {
        app.Run();
    }
    catch (Exception e)
    {
        logger.LogCritical(e, $"UnhandledExceptionEvent occured, Message: {e.Message}");
    }
});

try
{
    app.Services.GetRequiredService<IRun>().Start();
}
catch (Exception e)
{
    logger.LogCritical(e, $"UnhandledExceptionEvent occured, Message: {e.Message}");

}

void OnUnhandledExceptionEventHandler(object sender, UnhandledExceptionEventArgs e)
{
    logger.LogCritical(e.ExceptionObject as Exception, $"UnhandledExceptionEvent occured, IsTerminating: {e.IsTerminating}");
}
