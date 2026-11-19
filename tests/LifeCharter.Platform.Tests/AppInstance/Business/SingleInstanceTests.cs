using LifeCharter.Platform.AppInstance.Business;
using Microsoft.Extensions.Logging.Abstractions;

namespace LifeCharter.Platform.Tests.AppInstance.Business;

public sealed class SingleInstanceTests
{
    private readonly string pipeName = "lifecharter-test-" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task Forwarded_command_gets_the_reply()
    {
        await using var instance = SingleInstance.Listen(pipeName, command => Task.FromResult($"got {command}"), NullLogger.Instance);

        var reply = await Task.Run(() => SingleInstance.TryForward("log run 5 km", out var text, pipeName) ? text : null, TestContext.Current.CancellationToken);

        Assert.Equal("got log run 5 km", reply);
    }

    [Fact]
    public void Forwarding_without_a_running_instance_fails() =>
        Assert.False(SingleInstance.TryForward("peek", out _, pipeName));
}
