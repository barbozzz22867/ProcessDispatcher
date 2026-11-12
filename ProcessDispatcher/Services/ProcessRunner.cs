using System.Diagnostics;
using ProcessDispatcher.Models;

namespace ProcessDispatcher.Services;

public sealed class ProcessRunner
{
    private readonly ProcessLogger _logger;

    public ProcessRunner(ProcessLogger logger)
    {
        _logger = logger;
    }

    public async Task<ProcessLogEntry> RunAsync(
        string program,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        DateTime startedAt = DateTime.Now;

        var psi = new ProcessStartInfo
        {
            FileName = program,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        var stdout = new System.Text.StringBuilder();
        var stderr = new System.Text.StringBuilder();

        using var process = new Process { StartInfo = psi };

        // Читаем оба потока одновременно — иначе при заполнении буфера
        // процесс «встанет» и мы получим дедлок (классическая ошибка).
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!process.Start())
                throw new InvalidOperationException("Не удалось запустить процесс.");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var failEntry = new ProcessLogEntry
            {
                StartedAt = startedAt,
                Program = program,
                Arguments = arguments,
                Duration = stopwatch.Elapsed,
                Error = ex.Message
            };
            await _logger.WriteAsync(failEntry, cancellationToken);
            return failEntry;
        }

        // Ждём завершения либо тайм-аута, либо отмены.
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, cancellationToken);

        bool finishedNormally = false;
        try
        {
            await process.WaitForExitAsync(linked.Token);
            finishedNormally = true;
        }
        catch (OperationCanceledException)
        {
            // Внешняя отмена или тайм-аут.
        }

        if (!finishedNormally)
        {
            // Процесс ещё работает. Осознанно решаем его убить.
            // Kill(entireProcessTree: true) — гасим всю цепочку дочерних процессов,
            // потому что завершение только родителя часто оставляет «сирот».
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);

                // Даём системе время завершить процесс и собрать ExitCode.
                await process.WaitForExitAsync(CancellationToken.None);
            }
            catch (InvalidOperationException) { /* уже вышел */ }
            catch (Exception ex)
            {
                stderr.AppendLine($"[Kill error] {ex.Message}");
            }
        }

        // Даём обработчикам DataReceived дочитать буфер.
        // WaitForExitAsync в некоторых версиях .NET не дожидается асинхронных событий.
        process.WaitForExit();

        stopwatch.Stop();

        int? exitCode = null;
        try
        {
            if (process.HasExited)
                exitCode = process.ExitCode;
        }
        catch { /* процесс мог быть убит */ }

        var entry = new ProcessLogEntry
        {
            StartedAt = startedAt,
            Program = program,
            Arguments = arguments,
            Pid = process.Id,
            Duration = stopwatch.Elapsed,
            ExitCode = exitCode,
            Error = finishedNormally
                ? null
                : (timeoutCts.IsCancellationRequested
                    ? $"Превышено время выполнения ({timeout.TotalSeconds:F0} c), процесс завершён принудительно."
                    : "Операция отменена пользователем, процесс завершён принудительно.")
        };

        await _logger.WriteAsync(entry, cancellationToken);

        // Для отладки/показа результата — вернуть оба потока вызывающему.
        // Можно убрать параметры, если они не нужны.
        LastStdout = stdout.ToString();
        LastStderr = stderr.ToString();

        return entry;
    }

    // Сохранённые выводы последнего запуска, если их нужно показать.
    public string LastStdout { get; private set; } = "";
    public string LastStderr { get; private set; } = "";
}