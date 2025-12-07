using RedisProxy.Backend;
using RedisProxy.Backend.Data;
using RedisProxy.Backend.RespParser;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<IRespParser, RespParser>();

builder.Services.AddHostedService<TcpProxyWorker>();

var host = builder.Build();
host.Run();
