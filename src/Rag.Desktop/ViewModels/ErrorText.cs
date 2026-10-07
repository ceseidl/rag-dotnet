using System.ClientModel;
using System.Net.Http;
using Rag.Application.Abstractions;

namespace Rag.Desktop.ViewModels;

/// <summary>Erros do provedor em linguagem de gente.</summary>
public static class ErrorText
{
    public static string Describe(Exception ex, string profile) =>
        ex switch
        {
            OperationCanceledException => "Pergunta cancelada.",

            ProviderUnavailableException { InnerException:
                HttpRequestException } =>
                $"Não foi possível falar com o provedor ({profile})." +
                " Ollama: docker start ollama. Foundry: confira o" +
                " endpoint e a rede.",

            ProviderUnavailableException p =>
                "O provedor recusou ou está indisponível (429, 5xx" +
                " ou tempo esgotado)." + (p.RetryAfter is { } w
                    ? $" Tente de novo em {w.TotalSeconds:0} s."
                    : " Tente de novo em instantes."),

            ClientResultException { Status: 401 or 403 } =>
                "Provedor recusou a credencial (401/403). Confira" +
                " Ai:ApiKey nos user-secrets ou o papel de acesso.",

            ClientResultException { Status: 404 } =>
                "Modelo ou endpoint não encontrado (404). Confira o" +
                " nome do deployment/modelo e o Ai:Endpoint.",

            InvalidOperationException => ex.Message,

            _ => $"Erro inesperado: {ex.GetType().Name}: " +
                 ex.Message
        };
}
