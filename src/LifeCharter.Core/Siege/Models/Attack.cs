namespace LifeCharter.Core.Siege.Models;

/// <summary>
/// One node attacked on one day. <see cref="Threat"/> is the damage it deals if it stays
/// undefended (0 while a Capacity node is still within its grace period).
/// </summary>
public sealed record Attack(Guid NodeId, double Severity, int Threat, bool Defended);
