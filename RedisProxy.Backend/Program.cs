using RedisProxy.Backend.Data;
using RedisProxy.Backend.Hubs;
using RedisProxy.Backend.RespParser;
using RedisProxy.Backend.Services;
using RedisProxy.Backend.Workers;

var builder = WebApplication.CreateBuilder(args);

// 1. Services
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<IRespParser, RespParser>();
builder.Services.AddSingleton<IAdvisoryService, AdvisoryService>();

builder.Services.AddHostedService<TcpProxyWorker>();
builder.Services.AddHostedService<RedisMonitorWorker>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSignalR(); 

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");

// 3. Map Endpoints
app.MapControllers();
app.MapHub<MetricsHub>("/hubs/metrics");

app.Run();