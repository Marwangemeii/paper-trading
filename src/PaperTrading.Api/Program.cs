using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PaperTrading.Api;
using PaperTrading.Api.Endpoints;
using PaperTrading.Api.Engine;
using PaperTrading.Api.Feed;
using PaperTrading.Api.Persistence;
using PaperTrading.Api.Realtime;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<TradingOptions>()
    .Bind(builder.Configuration.GetSection(TradingOptions.Section))
    .Validate(o => o.Instruments.Count > 0, "At least one instrument must be configured.")
    .Validate(o => o.StartingCash > 0, "Starting cash must be positive.")
    .ValidateOnStart();
builder.Services.AddOptions<PriceFeedOptions>().Bind(builder.Configuration.GetSection(PriceFeedOptions.Section));

builder.Services.AddDbContextFactory<TradingDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Trading")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<EngineHost>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<EngineHost>());
builder.Services.AddSingleton<MockPriceFeed>();
builder.Services.AddSingleton<BinancePriceFeed>();
builder.Services.AddHostedService<PriceFeedService>();

// Same JSON rules for HTTP and SignalR: enums as names, money as exact strings.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.Converters.Add(new DecimalStringJsonConverter());
});
builder.Services.AddSignalR().AddJsonProtocol(o =>
{
    o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.PayloadSerializerOptions.Converters.Add(new DecimalStringJsonConverter());
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders(TradingEndpoints.ReplayedHeader)));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

// The interactive API page stays on in every environment because it is part of the demo.
app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthChecks("/health");
app.MapTradingApi();
app.MapHub<MarketHub>(MarketHub.Path);

app.Run();
