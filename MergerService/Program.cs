using MergerLogic.Extensions;
using MergerService.Src;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.RegisterMergerLogicType();
builder.Services.AddSingleton<IRun, Run>();

var app = builder.Build();

app.MapControllers();

new Thread(() =>
{
    app.Run();
});

app.Services.GetRequiredService<IRun>().Start();
