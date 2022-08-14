using MergerLogic.Extensions;
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

var app = builder.Build();

app.MapControllers();

new Thread(() =>
{
    app.Run();
});

app.Services.GetRequiredService<IRun>().Start();
