using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Infrastructure;

namespace Rag.Desktop.Services;

/// <summary>Monta o provedor de serviços do pipeline para um perfil
/// (Ollama ou Foundry) e uma pasta de documentos. A chave só vem de
/// user-secrets ou de variável de ambiente.</summary>
public static class RagHost
{
    public static IServiceProvider Build(
        string profile, string folder)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{profile}.json",
                optional: false)
            .AddUserSecrets(typeof(RagHost).Assembly,
                optional: true)
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Documents:Path"] = folder
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRag(config);
        return services.BuildServiceProvider();
    }
}
