using System.Collections.Concurrent;
using System.Threading.Channels;

namespace PCA.Web.Services;

public record CrbProgressEvent(
    string   T,           // "step" | "done" | "error"
    string   Msg,
    object?  Result = null);

public class CrbProgressStore
{
    private readonly ConcurrentDictionary<string, Channel<CrbProgressEvent>> _channels = new();

    public string CreateRun()
    {
        var runId = Guid.NewGuid().ToString("N")[..12];
        CreateRunWithId(runId);
        return runId;
    }

    public void CreateRunWithId(string runId)
    {
        _channels[runId] = Channel.CreateUnbounded<CrbProgressEvent>(
            new UnboundedChannelOptions { SingleReader = true });
    }

    public void Report(string runId, string message)
    {
        if (_channels.TryGetValue(runId, out var ch))
            ch.Writer.TryWrite(new CrbProgressEvent("step", message));
    }

    public void Complete(string runId, object result)
    {
        if (_channels.TryGetValue(runId, out var ch))
        {
            ch.Writer.TryWrite(new CrbProgressEvent("done", "Complete", result));
            ch.Writer.Complete();
        }
    }

    public void Fail(string runId, string error)
    {
        if (_channels.TryGetValue(runId, out var ch))
        {
            ch.Writer.TryWrite(new CrbProgressEvent("error", error));
            ch.Writer.Complete();
        }
    }

    public ChannelReader<CrbProgressEvent>? GetReader(string runId)
        => _channels.TryGetValue(runId, out var ch) ? ch.Reader : null;

    public void Remove(string runId)
        => _channels.TryRemove(runId, out _);
}
