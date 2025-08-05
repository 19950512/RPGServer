using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using RPGServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000, o => o.Protocols = HttpProtocols.Http2); // gRPC
    options.ListenAnyIP(5001); // HTTP normal para healthcheck
});

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<GameServiceImpl>();
app.MapGet("/", () => "Servidor RPG Online está rodando!");

app.Run();
