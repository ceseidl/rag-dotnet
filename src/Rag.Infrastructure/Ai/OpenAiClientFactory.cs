#pragma warning disable OPENAI001
using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using OpenAI;

namespace Rag.Infrastructure.Ai;

/// <summary>Único lugar que cria o cliente do provedor. Chave ou
/// identidade: o resto do código não muda.</summary>
public static class OpenAiClientFactory
{
    private const string Scope = "https://ai.azure.com/.default";

    public static OpenAIClient Create(
        AiOptions ai, HttpClient http)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(ai.Endpoint),
            // O retry fica no handler HTTP; o do SDK sairia
            // multiplicando as tentativas.
            RetryPolicy = new ClientRetryPolicy(0),
            Transport = new HttpClientPipelineTransport(http)
        };
        if (ai.Auth == "EntraId")
        {
            // Sem chave: az login, identidade gerenciada etc.
            var policy = new BearerTokenPolicy(
                new DefaultAzureCredential(), Scope);
            return new OpenAIClient(policy, options);
        }
        return new OpenAIClient(
            new ApiKeyCredential(ai.ApiKey!), options);
    }
}
