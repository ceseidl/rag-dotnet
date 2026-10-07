using Rag.Application.Ingestion;

namespace Rag.Api.Infrastructure;

/// <summary>Indexa os documentos ao subir (config:
/// Rag:IngestOnStartup). A consulta nunca ingere.</summary>
public sealed class IngestOnStartup(
    IngestionService ingestion,
    IConfiguration config,
    ILogger<IngestOnStartup> log) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!config.GetValue("Rag:IngestOnStartup", true)) return;
        var r = await ingestion.RunAsync(ct);
        log.LogInformation(
            "Ingestão: +{Added} ~{Updated} ={Same} -{Removed}",
            r.Added, r.Updated, r.Unchanged, r.Removed);
    }

    public Task StopAsync(CancellationToken ct) =>
        Task.CompletedTask;
}
