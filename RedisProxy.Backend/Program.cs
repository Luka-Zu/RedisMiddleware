using RedisProxy.Backend;
using RedisProxy.Backend.Data;
using RedisProxy.Backend.RespParser;
using RedisProxy.Backend.Workers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<IRespParser, RespParser>();

builder.Services.AddHostedService<TcpProxyWorker>();
builder.Services.AddHostedService<RedisMonitorWorker>();


var host = builder.Build();
host.Run();
