using LifeCharter.Core.Sync.Models;

namespace LifeCharter.Data.Sync.Models;

/// <summary>
/// Which remote snapshots a merge applied, the newest HLC stamp it saw, and how many local rows
/// it inserted or updated (0 when the snapshots held nothing new).
/// </summary>
public sealed record MergeReport(IReadOnlyList<string> MergedDevices, HlcStamp? MaxRemoteStamp, int RowsChanged = 0);
