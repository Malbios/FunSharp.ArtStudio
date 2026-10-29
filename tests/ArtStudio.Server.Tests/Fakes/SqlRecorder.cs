using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArtStudio.Server.Tests.Fakes;

public sealed record RecordedCommand(string Sql, IReadOnlyList<(string Name, object? Value)> Parameters);

/// <summary>
/// Records the SQL that requests sent with <see cref="Header"/> run, keyed by the header's value, so the app's
/// background workers never mix in.
/// </summary>
public sealed class SqlRecorder(IHttpContextAccessor http) : DbCommandInterceptor
{
    public const string Header = "X-Record-Sql";

    private static readonly ConcurrentDictionary<string, ConcurrentQueue<RecordedCommand>> Recordings = new();

    public static async Task<IReadOnlyList<RecordedCommand>> RecordAsync(Func<HttpRequestMessage, Task> send)
    {
        var id = Guid.NewGuid().ToString("N");
        var commands = Recordings.GetOrAdd(id, _ => new ConcurrentQueue<RecordedCommand>());
        try
        {
            using var request = new HttpRequestMessage();
            request.Headers.Add(Header, id);
            await send(request);
            return commands.ToList();
        }
        finally
        {
            Recordings.TryRemove(id, out _);
        }
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }

    private void Record(DbCommand command)
    {
        if (http.HttpContext?.Request.Headers[Header].ToString() is not { Length: > 0 } id
            || !Recordings.TryGetValue(id, out var commands))
            return;
        commands.Enqueue(new RecordedCommand(
            command.CommandText,
            command.Parameters.Cast<DbParameter>().Select(p => (p.ParameterName, p.Value == DBNull.Value ? null : p.Value)).ToList()));
    }
}
