using MergerLogic.Extensions;
using MergerLogic.Clients;
using MergerService.Src;
using MergerService.Utils;
using System.Net;

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

new Thread(() =>
{
    app.Run();
});

ServicePointManager.DefaultConnectionLimit = 100;
app.Services.GetRequiredService<IRun>().Start();
