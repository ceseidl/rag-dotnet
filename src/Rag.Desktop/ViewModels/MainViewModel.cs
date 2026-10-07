using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Rag.Application;
using Rag.Application.Ingestion;
using Rag.Application.Querying;
using Rag.Domain;

namespace Rag.Desktop.ViewModels;

public sealed record SourceItem(
    int Number, string Title, string Where, string Score);

public sealed record MetricItem(string Name, string Value);

/// <summary>MVVM simples. Chama o pipeline REAL (o mesmo da API):
/// busca nos documentos, prompt e provedor. A fábrica recebe o
/// perfil e a pasta e devolve o provedor de serviços.</summary>
public sealed class MainViewModel : Observable
{
    private readonly
        Func<string, string, IServiceProvider> _build;
    private IServiceProvider? _sp;
    private string _key = "";
    private bool _indexed;
    private CancellationTokenSource? _cts;
    private string _profile, _folder, _question = "";
    private string _answer = "", _error = "", _status = "";
    private bool _busy, _answered;
    private AccessLevel _access = AccessLevel.Team;

    public MainViewModel(
        Func<string, string, IServiceProvider> build,
        string profile = "Ollama", string folder = "docs-exemplo")
    {
        _build = build;
        _profile = profile;
        _folder = folder;
        Ask = new(AskAsync, () => Question.Trim().Length >= 3);
        Reindex = new(ReindexAsync);
        Cancel = new(() => _cts?.Cancel(), () => IsBusy);
    }

    public IReadOnlyList<string> Profiles { get; } =
        ["Ollama", "Foundry"];

    public IReadOnlyList<AccessLevel> Levels { get; } =
        Enum.GetValues<AccessLevel>();

    public ObservableCollection<SourceItem> Sources { get; } = [];
    public ObservableCollection<MetricItem> Metrics { get; } = [];
    public AsyncCommand Ask { get; }
    public AsyncCommand Reindex { get; }
    public RelayCommand Cancel { get; }

    public string Profile
    {
        get => _profile;
        set => Set(ref _profile, value);
    }

    public string Folder
    {
        get => _folder;
        set => Set(ref _folder, value);
    }

    public AccessLevel Access
    {
        get => _access;
        set => Set(ref _access, value);
    }

    public string Question
    {
        get => _question;
        set { Set(ref _question, value); Ask.Refresh(); }
    }

    public string Answer
    {
        get => _answer;
        private set => Set(ref _answer, value);
    }

    public bool Answered
    {
        get => _answered;
        private set => Set(ref _answered, value);
    }

    public string Error
    {
        get => _error;
        private set => Set(ref _error, value);
    }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public bool IsBusy
    {
        get => _busy;
        private set
        {
            Set(ref _busy, value);
            Cancel.Refresh();
        }
    }

    private IServiceProvider Provider()
    {
        var key = $"{Profile}|{Folder}";
        if (_sp is null || key != _key)
        {
            (_sp as IDisposable)?.Dispose();
            _sp = _build(Profile, Folder);
            _key = key;
            _indexed = false;
        }
        return _sp;
    }

    private async Task ReindexAsync()
    {
        Begin();
        try { await IndexAsync(_cts!.Token); }
        catch (Exception ex) { Fail(ex); }
        finally { IsBusy = false; }
    }

    private async Task IndexAsync(CancellationToken ct)
    {
        var sp = Provider();
        Status = "Indexando os documentos...";
        var clock = Stopwatch.StartNew();
        var r = await sp.GetRequiredService<IngestionService>()
            .RunAsync(ct);
        _indexed = true;
        Status = $"Índice pronto: {r.Chunks} trechos novos " +
            $"({r.Unchanged} documentos sem mudança) em " +
            $"{clock.Elapsed.TotalSeconds:0.0} s.";
    }

    private async Task AskAsync()
    {
        Begin();
        Answer = "";
        Answered = false;
        Sources.Clear();
        Metrics.Clear();
        var stages = new List<(string Name, double Ms)>();
        using var listener = Listen(stages);
        try
        {
            var ct = _cts!.Token;
            var sp = Provider();
            if (!_indexed) await IndexAsync(ct);
            Status = $"Perguntando ao provedor ({Profile})...";
            var clock = Stopwatch.StartNew();
            var r = await sp.GetRequiredService<AskService>()
                .AskAsync(new(Question.Trim(), Access), ct);
            Show(r, clock.Elapsed.TotalSeconds, stages);
        }
        catch (Exception ex) { Fail(ex); }
        finally { IsBusy = false; }
    }

    private void Show(
        AskResult r, double secs,
        List<(string Name, double Ms)> st)
    {
        Answer = r.Answer;
        Answered = r.Answered;
        foreach (var c in r.Citations)
            Sources.Add(new(c.Number, c.Title,
                $"{c.Source} v{c.Version} > {c.Section}",
                c.Score.ToString("0.000")));

        foreach (var name in new[] { "embed-query", "retrieve",
                     "generate" })
            foreach (var s in st.Where(
                         x => x.Name == "rag." + name))
                Metrics.Add(new(name, $"{s.Ms:0} ms"));
        var d = r.Diagnostics;
        Metrics.Add(new("trechos candidatos", $"{d.Candidates}"));
        Metrics.Add(new("trechos no prompt", $"{d.Used}"));
        Metrics.Add(new("tokens entrada/saída",
            $"{d.InputTokens?.ToString() ?? "-"} / " +
            $"{d.OutputTokens?.ToString() ?? "-"}"));
        Metrics.Add(new("ancoragem", $"{d.Grounding:P0}"));
        Metrics.Add(new("tempo total", $"{secs:0.0} s"));
        Status = r.Answered ? "Resposta com fontes."
            : "O provedor não encontrou base nos documentos.";
    }

    private void Begin()
    {
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        Error = "";
        IsBusy = true;
    }

    private void Fail(Exception ex)
    {
        Status = "";
        Error = ErrorText.Describe(ex, Profile);
        if (_sp is not null && ex is InvalidOperationException)
            _key = "";
    }

    private static ActivityListener Listen(
        List<(string Name, double Ms)> into)
    {
        var l = new ActivityListener
        {
            ShouldListenTo = s => s.Name == RagTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext>
                _) => ActivitySamplingResult.AllData,
            ActivityStopped = a =>
            {
                lock (into)
                    into.Add((a.OperationName,
                        a.Duration.TotalMilliseconds));
            }
        };
        ActivitySource.AddActivityListener(l);
        return l;
    }
}
