using AspNetCoreRateLimit;
using Serilog;
using VNUFLearning.Gateway.Middleware;
using VNUFLearning.Gateway.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Serilog
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.Console()
       .WriteTo.File("logs/gateway-.log", rollingInterval: RollingInterval.Day));

// YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(configuration.GetSection("ReverseProxy"));

// Token validator (singleton)
builder.Services.AddSingleton<TokenValidator>();

// CORS
builder.Services.AddCors(opts =>
    opts.AddPolicy("AllowOrigin", p =>
        p.WithOrigins(
            configuration.GetSection("Gateway:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5153" })
         .AllowAnyMethod()
         .AllowAnyHeader()
         .AllowCredentials()));

// Rate limiting (per IP)
builder.Services.AddOptions();
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Forwarded headers (so client IP is real when behind proxy)
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(opts =>
{
    opts.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

// Middleware pipeline
app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseIpRateLimiting();
app.UseCors("AllowOrigin");

// Gateway-specific middleware
app.UseMiddleware<JwtValidationMiddleware>();
app.UseMiddleware<SetApiKeyMiddleware>();

app.MapReverseProxy();

app.Run();
