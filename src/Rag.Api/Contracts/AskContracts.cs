using System.ComponentModel.DataAnnotations;
using Rag.Application.Querying;
using Rag.Domain;

namespace Rag.Api.Contracts;

public sealed record AskBody(
    [property: Required(AllowEmptyStrings = false)]
    [property: StringLength(500, MinimumLength = 3)]
    string Question);

public sealed record AskResponse(
    string Answer,
    bool Answered,
    IReadOnlyList<Citation> Citations,
    int ChunksUsed,
    double Grounding)
{
    public static AskResponse From(AskResult r) => new(
        r.Answer, r.Answered, r.Citations,
        r.Diagnostics.Used, r.Diagnostics.Grounding);
}
