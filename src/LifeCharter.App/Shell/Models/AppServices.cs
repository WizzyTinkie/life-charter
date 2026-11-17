using LifeCharter.Core.Common.Business;
using LifeCharter.Data.Storage.Business;

namespace LifeCharter.App.Shell.Models;

/// <summary>The long-lived services the shell hands to windows.</summary>
public sealed record AppServices(string DeviceId, LifeCharterDbContextFactory ContextFactory, ChartSession Session);
