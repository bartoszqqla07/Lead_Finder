namespace LeadFinder.Models;

public enum ScoreTier
{
    Low,
    Medium,
    High,
}

/// <summary>Jeden składnik oceny, np. ("Strona nie działa…", +45).</summary>
public sealed record ScoreFactor(string Label, int Points);

/// <summary>
/// Szacunek szansy, że firma zechce nową stronę: 0–100 z listą składników,
/// żeby było widać, skąd wziął się wynik.
/// </summary>
public sealed record LeadScore(int Value, ScoreTier Tier, IReadOnlyList<ScoreFactor> Factors);
