using MergerLogic.Extentions;
using MergerService.Src;
using MergerService.Utils;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.RegisterMergerLogicType();
builder.Services.AddSingleton<IRun, Run>();
builder.Services.AddSingleton<ITaskUtils, TaskUtils>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

Console.WriteLine(Environment.CurrentDirectory);
Console.WriteLine(AppContext.BaseDirectory);

// new Thread(() =>
// {
//     app.Run();
// });

// app.Services.GetRequiredService<IRun>().Start();
