using ProcessDispatcher.Models;

namespace ProcessDispatcher.Services;

public sealed class ProcessLogger
{
    private readonly string _logPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ProcessLogger(string logPath)
    {
        _logPath = logPath;
    }

    public async Task WriteAsync(ProcessLogEntry entry, CancellationToken ct = default)
    {
        // Несколько логов могут писаться параллельно — сериализуем запись.
        await _lock.WaitAsync(ct);
        try
        {
            await File.AppendAllTextAsync(_logPath, entry.ToString(), ct);
        }
        finally
        {
            _lock.Release();
        }
    }
}