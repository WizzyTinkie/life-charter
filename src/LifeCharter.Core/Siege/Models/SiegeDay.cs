namespace LifeCharter.Core.Siege.Models;

/// <summary>The attacks of one day, interleaved across attributes, and how they went.</summary>
public sealed record SiegeDay(DateOnly Date, SiegeDayKind Kind, IReadOnlyList<Attack> Attacks, bool Shielded)
{
    public int Defended => Attacks.Count(attack => attack.Defended);

    /// <summary>Damage dealt by the undefended attacks (for today: what they would deal).</summary>
    public int Damage => Shielded ? 0 : Attacks.Where(attack => !attack.Defended).Sum(attack => attack.Threat);
}
