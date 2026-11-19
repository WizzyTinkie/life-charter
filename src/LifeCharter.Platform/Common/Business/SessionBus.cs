using Tmds.DBus.Protocol;

namespace LifeCharter.Platform.Common.Business;

/// <summary>Small helpers for calls on the D-Bus session bus.</summary>
internal static class SessionBus
{
    public static async Task<DBusConnection> ConnectAsync()
    {
        var address = DBusAddress.Session ?? throw new InvalidOperationException("No D-Bus session bus.");
        var connection = new DBusConnection(address);
        await connection.ConnectAsync().ConfigureAwait(false);
        return connection;
    }

    public static Task CallAsync(
        DBusConnection connection, string destination, string path, string @interface, string member, string? signature, MessageBodyWriter? body = null) =>
        connection.CallMethodAsync(Build(connection, destination, path, @interface, member, signature, body));

    public static Task<T> CallAsync<T>(
        DBusConnection connection, string destination, string path, string @interface, string member, string? signature,
        MessageBodyWriter? body, MessageValueReader<T> reader) =>
        connection.CallMethodAsync(Build(connection, destination, path, @interface, member, signature, body), reader);

    // MessageWriter is a ref struct: write, create and dispose the same instance, never a copy.
    private static MessageBuffer Build(
        DBusConnection connection, string destination, string path, string @interface, string member, string? signature, MessageBodyWriter? body)
    {
        var writer = connection.GetMessageWriter();
        try
        {
            writer.WriteMethodCallHeader(destination, path, @interface, member, signature, MessageFlags.None);
            body?.Invoke(ref writer);
            return writer.CreateMessage();
        }
        finally
        {
            writer.Dispose();
        }
    }
}
