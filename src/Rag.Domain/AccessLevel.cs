namespace Rag.Domain;

/// <summary>Quem pode ler o documento e seus trechos.</summary>
public enum AccessLevel
{
    Public = 0,
    Team = 1,
    Restricted = 2
}
