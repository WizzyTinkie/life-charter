using System.IO.Pipes;
using Microsoft.Extensions.Logging;

namespace LifeCharter.Platform.AppInstance.Business;

/// <summary>
/// Keeps one running LifeCharter per user. Later launches (<c>lifecharter peek</c>, KDE
/// shortcuts, scripts) forward their command over a named pipe and exit. On Linux .NET maps
/// named pipes to Unix domain sockets, so no network port is opened.
/// </summary>
public sealed class SingleInstance : IAsyncDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(300);

    private readonly CancellationTokenSource stopping = new();
    private readonly Task listener;

    // Task.Run keeps the listener off the caller's synchronization context (the UI thread),
    // so disposing from the UI thread cannot deadlock on it.
    private SingleInstance(Func<string, Task> onCommand, ILogger logger) =>
        listener = Task.Run(() => ListenAsync(onCommand, logger, stopping.Token));

    private static string PipeName => "lifecharter-" + Environment.UserName;

    /// <summary>Sends <paramref name="command"/> to an already running instance.</summary>
    /// <remarks>
    /// Synchronous on purpose: it runs in <c>Program.Main</c>, which must stay synchronous to
    /// keep the STA thread Avalonia needs on Windows (see the async rules in CODING_GUIDELINES.md).
    /// </remarks>
    /// <returns><c>true</c> when another instance received it.</returns>
    public static bool TryForward(string command)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(ConnectTimeout);
            using var writer = new StreamWriter(client);
            writer.WriteLine(command);
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            return false;
        }
    }

    public static SingleInstance Listen(Func<string, Task> onCommand, ILogger logger) => new(onCommand, logger);

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await listener.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        stopping.Dispose();
    }

    private static async Task ListenAsync(Func<string, Task> onCommand, ILogger logger, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server);
                var command = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(command))
                {
                    logger.LogInformation("Received command {Command} from another launch", command);
                    await onCommand(command.Trim()).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Single-instance listener failed; retrying");
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
