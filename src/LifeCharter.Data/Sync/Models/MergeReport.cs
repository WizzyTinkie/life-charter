using LifeCharter.Core.Sync.Models;

namespace LifeCharter.Data.Sync.Models;

/// <summary>Which remote snapshots a merge applied, and the newest HLC stamp it saw.</summary>
public sealed record MergeReport(IReadOnlyList<string> MergedDevices, HlcStamp? MaxRemoteStamp);
