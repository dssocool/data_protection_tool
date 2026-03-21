using DataProtectionTool.Master.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Prefer explicit configuration (local launchSettings, Azure env, etc.).
var configuredUrls = builder.Configuration["ASPNETCORE_URLS"]
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (string.IsNullOrWhiteSpace(configuredUrls))
{
    // Azure Container Apps commonly sets PORT; default for local parity.
    var port = Environment.GetEnvironmentVariable("PORT") ?? "5055";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        // gRPC requires HTTP/2; allow HTTP/1 for simple GET / health text.
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<MasterGrpcService>();
app.MapGet("/", () => Results.Text(
    "DataProtectionTool.Master gRPC. Use HTTP/2 (h2c) on this port for gRPC clients."));

app.Run();
