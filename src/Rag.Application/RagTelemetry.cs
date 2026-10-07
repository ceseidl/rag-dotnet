using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Rag.Application;

/// <summary>Spans e métricas do pipeline. A API só liga o
/// OpenTelemetry nestas duas fontes (nome "Rag").</summary>
public static class RagTelemetry
{
    public const string Name = "Rag";
    public const string AiName = "Rag.Ai";

    public static readonly ActivitySource Source = new(Name);
    private static readonly Meter Meter = new(Name);

    public static readonly Histogram<double> StageMs =
        Meter.CreateHistogram<double>("rag.stage.duration", "ms");
    public static readonly Counter<long> Chunks =
        Meter.CreateCounter<long>("rag.chunks.retrieved");
    public static readonly Counter<long> Tokens =
        Meter.CreateCounter<long>("rag.tokens");
    public static readonly Counter<long> Abstentions =
        Meter.CreateCounter<long>("rag.abstentions");
    public static readonly Counter<long> Blocked =
        Meter.CreateCounter<long>("rag.guard.blocked");

    /// <summary>Abre um span e grava a duração.</summary>
    public static Stage Start(string stage) => new(stage);

    public readonly struct Stage : IDisposable
    {
        private readonly string _name;
        private readonly Activity? _activity;
        private readonly long _start;

        public Stage(string name)
        {
            _name = name;
            _activity = Source.StartActivity($"rag.{name}");
            _start = Stopwatch.GetTimestamp();
        }

        public void Tag(string key, object? value) =>
            _activity?.SetTag(key, value);

        public void Dispose()
        {
            var ms = Stopwatch.GetElapsedTime(_start)
                .TotalMilliseconds;
            StageMs.Record(ms, new KeyValuePair<string, object?>(
                "stage", _name));
            _activity?.Dispose();
        }
    }
}
