using CommunityToolkit.Mvvm.Input;
using LifeCharter.App.Siege.Business;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.App.Siege.ViewModels;

/// <summary>The retention part of the side panel of a mastered node.</summary>
public sealed partial class RetentionViewModel
{
    private readonly ChartSession session;
    private readonly Guid nodeId;

    public RetentionViewModel(ChartSession session, SkillNode node, NodeRetention retention)
    {
        this.session = session;
        nodeId = node.Id;

        var status = retention.Status;
        ModelText = RetentionText.Model(node.RetentionModel);
        Integrity = retention.Integrity;
        IsCorrupted = retention.IsCorrupted;
        IntegrityText = retention.IsCorrupted
            ? $"Cleanse: {retention.CleanseDone} / {retention.CleanseRequired} practice sessions restore it" +
              (retention.RewardWithheld ? $" (its +{node.XpReward} XP is withheld until then)" : "")
            : $"Integrity {retention.Integrity} / 100";
        ShowProgress = status.Model != RetentionModel.Milestone;
        ProgressTitle = status.IsEngraved ? "Engraved — learned for good, never attacked again" : RetentionText.ProgressTitle(status);
        Fraction = status.Fraction;
        Criteria = status.Criteria.Select(RetentionText.Criterion).ToList();
        EstimateText = RetentionText.Estimate(status);

        var attack = session.Progression.Siege.Today.Attacks.FirstOrDefault(a => a.NodeId == node.Id);
        AttackText = attack is null ? "" : $"Attacked today · {RetentionText.Attack(attack)}";
        CanRecall = node.RetentionModel is (RetentionModel.Memory or RetentionModel.MotorSkill) && !status.IsEngraved;
        SelfCheck = status.NeedsSelfCheck ? new SelfCheckViewModel(session, node.Id, node.Title) : null;
    }

    public string ModelText { get; }

    public int Integrity { get; }

    public bool IsCorrupted { get; }

    public string IntegrityText { get; }

    public bool ShowProgress { get; }

    public string ProgressTitle { get; }

    public double Fraction { get; }

    public IReadOnlyList<string> Criteria { get; }

    public string EstimateText { get; }

    public bool HasEstimate => EstimateText.Length > 0;

    public string AttackText { get; }

    public bool IsAttacked => AttackText.Length > 0;

    public bool CanRecall { get; }

    public SelfCheckViewModel? SelfCheck { get; }

    public bool NeedsSelfCheck => SelfCheck is not null;

    [RelayCommand]
    private Task RecallAsync(ReviewRating rating) => session.ReviewAsync(nodeId, rating, EntrySource.Manual);
}
