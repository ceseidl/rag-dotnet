using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Rag.Api;
using Rag.Api.Endpoints;
using Rag.Api.Infrastructure;
using Rag.Api.Security;
using Rag.Application;
using Rag.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Perfil de provedor: Profile=Ollama carrega o arquivo
// appsettings.Ollama.json (ou Foundry). Sem perfil: Offline.
if (builder.Configuration["Profile"] is { Length: > 0 } profile)
    builder.Configuration.AddJsonFile(
        $"appsettings.{profile}.json", optional: false);

builder.Services.AddRag(builder.Configuration);
builder.Services.AddHostedService<IngestOnStartup>();

// Validação (DataAnnotations) nativa do Minimal API,
// ProblemDetails e tradução do erro do provedor para 503.
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks();

builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1);
    o.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// Autenticação: o esquema de desenvolvimento só existe em Dev.
if (!builder.Environment.IsDevelopment())
    throw new InvalidOperationException(
        "Configure JwtBearer: DevHeader é só para dev.");
builder.Services.AddAuthentication(DevHeaderHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DevHeaderHandler>(
        DevHeaderHandler.SchemeName, null);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RagAdmin", p => p.RequireRole("rag-admin"));

// Limite por usuário: protege a cota do provedor e o custo.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("ask", http =>
        RateLimitPartition.GetTokenBucketLimiter(
            http.User.Identity?.Name ?? "anon", _ => new()
            {
                TokenLimit = 10,
                TokensPerPeriod = 10,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1)
            }));
});

// Observabilidade: spans e métricas do pipeline e da IA.
var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("rag-api"))
    .WithTracing(t => t
        .AddSource(RagTelemetry.Name, RagTelemetry.AiName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(m => m
        .AddMeter(RagTelemetry.Name, RagTelemetry.AiName)
        .AddAspNetCoreInstrumentation());
if (builder.Configuration.GetValue<bool>("Telemetry:Console"))
    otel.WithTracing(t => t.AddConsoleExporter());
if (!string.IsNullOrEmpty(
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
    otel.UseOtlpExporter();

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapRagEndpoints();

if (args.Contains("--eval"))
    return await EvalCommand.RunAsync(app);
if (args.Contains("--ask"))
    return await AskCommand.RunAsync(app, args);

app.Run();
return 0;

public partial class Program;
