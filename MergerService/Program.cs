using MergerLogic.Extensions;
using MergerLogic.Clients;
using MergerService.Src;
using MergerService.Utils;

string reflectionLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
    ContentRootPath = reflectionLocation,
    Args = args
});

var b = builder.Configuration["hostBuilder:reloadConfigOnChange"];
builder.Configuration["hostBuilder:reloadConfigOnChange"] = "false";

string workingDirectory = Path.GetFullPath(Directory.GetCurrentDirectory());
Console.WriteLine($"reflectionLocation: {Path.GetDirectoryName(reflectionLocation)}");
Console.WriteLine($"System.IO Location: {Path.GetDirectoryName(workingDirectory)}");
Console.WriteLine($"System.Environment.CurrentDirectory: {System.Environment.CurrentDirectory}");
Console.WriteLine($"ContentRoot Path: {builder.Environment.ContentRootPath}");
Console.WriteLine($"WebRootPath: {builder.Environment.WebRootPath}");
Console.WriteLine($"ApplicationName: {builder.Environment.ApplicationName}");
Console.WriteLine($"EnvironmentName: {builder.Environment.EnvironmentName}");

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
