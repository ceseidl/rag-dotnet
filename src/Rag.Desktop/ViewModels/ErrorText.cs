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
                $"Sem conexão com o provedor ({profile}). " +
                "Ollama: docker start ollama. " +
                "Foundry: confira o endpoint e a rede.",

            ProviderUnavailableException p =>
                "O provedor recusou ou está indisponível " +
                "(429, 5xx ou tempo esgotado)." +
                (p.RetryAfter is { } w
                    ? $" Tente de novo em {w.TotalSeconds:0} s."
                    : " Tente de novo em instantes."),

            ClientResultException { Status: 401 or 403 } =>
                "Credencial recusada (401/403). Confira o " +
                "Ai:ApiKey (user-secrets) ou o papel.",

            ClientResultException { Status: 404 } =>
                "Modelo ou endpoint não achado (404). Confira " +
                "o deployment e o Ai:Endpoint.",

            InvalidOperationException => ex.Message,

            _ => $"Erro inesperado: {ex.GetType().Name}: " +
                 ex.Message
        };
}
