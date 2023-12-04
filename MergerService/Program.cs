using MergerLogic.Clients;
using MergerLogic.Extensions;
using MergerService.Src;
using MergerService.Utils;
using System.Reflection;

string reflectionLocation = Assembly.GetExecutingAssembly().Location;
string reflectionLocationDir = Path.GetDirectoryName(reflectionLocation);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = reflectionLocationDir,
    WebRootPath = reflectionLocationDir,
    Args = args
});

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

Console.WriteLine($"ContentRoot Path: {builder.Environment.ContentRootPath}");
Console.WriteLine($"WebRootPath: {builder.Environment.WebRootPath}");

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
    try
    {
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
