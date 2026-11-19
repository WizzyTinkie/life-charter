using System.IO.Pipes;
using Microsoft.Extensions.Logging;

namespace LifeCharter.Platform.AppInstance.Business;

/// <summary>
/// Keeps one running LifeCharter per user. Later launches (<c>lifecharter peek</c>,
/// <c>lifecharter log "run 5 km"</c>, scripts) forward their command over a named pipe, print
/// the running instance's one-line reply and exit. On Linux .NET maps named pipes to Unix domain
/// sockets, so no network port is opened.
/// </summary>
public sealed class SingleInstance : IAsyncDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(300);

    private readonly CancellationTokenSource stopping = new();
    private readonly Task listener;

    // Task.Run keeps the listener off the caller's synchronization context (the UI thread),
    // so disposing from the UI thread cannot deadlock on it.
    private SingleInstance(string pipeName, Func<string, Task<string>> onCommand, ILogger logger) =>
        listener = Task.Run(() => ListenAsync(pipeName, onCommand, logger, stopping.Token));

    private static string DefaultPipeName => "lifecharter-" + Environment.UserName;

    /// <summary>Sends <paramref name="command"/> to an already running instance.</summary>
    /// <remarks>
    /// Synchronous on purpose: it runs in <c>Program.Main</c>, which must stay synchronous to
    /// keep the STA thread Avalonia needs on Windows (see the async rules in CODING_GUIDELINES.md).
    /// </remarks>
    /// <returns><c>true</c> when another instance received it; <paramref name="reply"/> is its answer (may be empty).</returns>
    public static bool TryForward(string command, out string reply) => TryForward(command, out reply, DefaultPipeName);

    internal static bool TryForward(string command, out string reply, string pipeName)
    {
        reply = "";
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
            client.Connect(ConnectTimeout);
            using (var writer = new StreamWriter(client, leaveOpen: true))
                writer.WriteLine(command);

            using var reader = new StreamReader(client);
            reply = reader.ReadToEnd().Trim();
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            return false;
        }
    }

    /// <summary>Listens for forwarded commands; the text <paramref name="onCommand"/> returns goes back to the caller.</summary>
    public static SingleInstance Listen(Func<string, Task<string>> onCommand, ILogger logger) => new(DefaultPipeName, onCommand, logger);

    internal static SingleInstance Listen(string pipeName, Func<string, Task<string>> onCommand, ILogger logger) => new(pipeName, onCommand, logger);

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

    private static async Task ListenAsync(string pipeName, Func<string, Task<string>> onCommand, ILogger logger, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server, leaveOpen: true);
                var command = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                var reply = "";
                if (!string.IsNullOrWhiteSpace(command))
                {
                    logger.LogInformation("Received command {Command} from another launch", command);
                    reply = await onCommand(command.Trim()).ConfigureAwait(false);
                }

                // Always answer, even with nothing, so the caller never waits for a reply that does not come.
                await using var writer = new StreamWriter(server, leaveOpen: true);
                await writer.WriteLineAsync(reply.AsMemory(), cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
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
