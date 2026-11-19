using LifeCharter.App.Shell.Models;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.SkillTree.Business;
using LifeCharter.Core.Sync.Business;
using LifeCharter.Data.Storage.Business;
using LifeCharter.Platform.Storage.Business;

namespace LifeCharter.App.Shell.Business;

/// <summary>Composition root: device identity → clock → database → chart session.</summary>
public static class AppServicesFactory
{
    public static async Task<AppServices> CreateAsync(CancellationToken cancellationToken = default)
    {
        var deviceId = await DeviceIdentity.LoadOrCreateAsync(AppPaths.ConfigDirectory, cancellationToken);
        Directory.CreateDirectory(AppPaths.DataDirectory);

        var clock = new HybridLogicalClock(deviceId);
        var contextFactory = new LifeCharterDbContextFactory(AppPaths.DatabasePath, clock);
        var store = new LifeChartStore(contextFactory);
        var session = new ChartSession(store, TimeProvider.System);

        await store.InitializeAsync(StarterTemplate.Create(session.Today), cancellationToken);
        await session.ReloadAsync(cancellationToken);
        await session.StartSiegeIfNeededAsync(cancellationToken);
        return new AppServices(deviceId, contextFactory, session);
    }
}
