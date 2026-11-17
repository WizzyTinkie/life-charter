namespace LifeCharter.Core.Progression.Models;

/// <summary>Why a node cannot be allocated right now; <see cref="None"/> means it can.</summary>
public enum AllocationBlock
{
    None,
    Root,
    AlreadyAllocated,
    NoGoal,
    NoSkillPoints,
    NotConnected,
    RequirementsUnmet,
    WorkInProgressLimit,
}
