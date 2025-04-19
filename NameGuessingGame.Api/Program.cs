using Microsoft.EntityFrameworkCore;
using NameGuessingGame.Api.Hubs;
using NameGuessingGame.Api.Data;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 添加 In-Memory 資料庫
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("NameGuessingGame"));

// 添加SignalR
builder.Services.AddSignalR();

// 添加CORS策略
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", builder =>
    {
        builder
               .SetIsOriginAllowed(_ => true) // 允許任何來源
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowVueApp");

app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/gamehub");

app.Run(); 