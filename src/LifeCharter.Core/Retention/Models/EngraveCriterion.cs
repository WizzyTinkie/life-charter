namespace LifeCharter.Core.Retention.Models;

/// <summary>One condition on the way to Engraved (or Seasoned), with where you stand.</summary>
public sealed record EngraveCriterion(EngraveCriterionKind Kind, double Current, double Target)
{
    public bool IsMet => Current >= Target;

    public double Fraction => Target <= 0 ? 1 : Math.Clamp(Current / Target, 0, 1);
}
