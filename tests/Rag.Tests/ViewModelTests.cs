using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rag.Application.Abstractions;
using Rag.Desktop.ViewModels;
using Rag.Domain;
using Rag.Infrastructure;
using Rag.Tests.Support;

namespace Rag.Tests;

/// <summary>O ViewModel do app WPF, com o pipeline de verdade e os
/// fakes (só aqui, nos testes) no lugar do provedor.</summary>
public class ViewModelTests
{
    private static Func<string, string, IServiceProvider> Build(
        Action<IServiceCollection>? tweak = null) =>
        (profile, folder) =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Ai:Mode"] = "Offline",
                    ["Documents:Path"] = Rig.DocsFolder()
                }).Build();
            var s = new ServiceCollection();
            s.AddLogging();
            s.AddRag(config);
            tweak?.Invoke(s);
            return s.BuildServiceProvider();
        };

    [Fact]
    public async Task Ask_shows_answer_sources_and_metrics()
    {
        var vm = new MainViewModel(Build())
        {
            Question = "Posso fazer deploy na sexta-feira?"
        };

        await vm.Ask.ExecuteAsync();

        Assert.True(vm.Answered);
        Assert.Contains("[1]", vm.Answer);
        var s = Assert.Single(vm.Sources);
        Assert.Contains("politica-de-deploy", s.Where);
        Assert.Contains(vm.Metrics, m => m.Name == "retrieve");
        Assert.Contains(vm.Metrics, m => m.Name == "tempo total");
        Assert.Equal("", vm.Error);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Provider_429_is_shown_with_retry_after()
    {
        var vm = new MainViewModel(Build(s =>
        {
            s.RemoveAll<IChatClient>();
            s.AddSingleton<IChatClient>(new FailingChat());
        })) { Question = "Posso fazer deploy na sexta-feira?" };

        await vm.Ask.ExecuteAsync();

        Assert.Contains("7 s", vm.Error);
        Assert.Equal("", vm.Answer);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Missing_provider_shows_what_to_configure()
    {
        var vm = new MainViewModel((_, _) =>
            throw new InvalidOperationException(
                "Nenhum provedor de IA configurado. Suba o Ollama."))
        {
            Question = "Posso fazer deploy na sexta-feira?"
        };

        await vm.Ask.ExecuteAsync();

        Assert.Contains("Nenhum provedor", vm.Error);
    }

    [Fact]
    public async Task Reindex_reports_the_chunks()
    {
        var vm = new MainViewModel(Build());

        await vm.Reindex.ExecuteAsync();

        Assert.Contains("Índice pronto", vm.Status);
    }

    [Fact]
    public async Task Access_level_filters_the_sources()
    {
        var vm = new MainViewModel(Build())
        {
            Question = "Como uso o acesso break-glass ao banco?",
            Access = AccessLevel.Public
        };

        await vm.Ask.ExecuteAsync();

        Assert.False(vm.Answered);
        Assert.Empty(vm.Sources);
    }

    private sealed class FailingChat : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> m, ChatOptions? o = null,
            CancellationToken c = default) =>
            throw new ProviderUnavailableException(
                "429", TimeSpan.FromSeconds(7));

        public IAsyncEnumerable<ChatResponseUpdate>
            GetStreamingResponseAsync(
            IEnumerable<ChatMessage> m, ChatOptions? o = null,
            CancellationToken c = default) =>
            throw new NotSupportedException();

        public object? GetService(Type t, object? k = null) => null;
        public void Dispose() { }
    }
}
