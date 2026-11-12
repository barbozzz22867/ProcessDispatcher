namespace ProcessDispatcher.Models;

public sealed class ProcessLogEntry
{
    public DateTime StartedAt { get; init; }
    public string Program { get; init; } = "";
    public string Arguments { get; init; } = "";
    public int? Pid { get; init; }
    public TimeSpan Duration { get; init; }
    public int? ExitCode { get; init; }
    public string? Error { get; init; }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"[{StartedAt:yyyy-MM-dd HH:mm:ss}]");
        sb.AppendLine($"Program: {Program}");
        sb.AppendLine($"Arguments: {Arguments}");

        if (Pid.HasValue)
            sb.AppendLine($"PID: {Pid}");

        sb.AppendLine($"Duration: {Duration:hh\\:mm\\:ss\\.fff}");

        if (ExitCode.HasValue)
            sb.AppendLine($"ExitCode: {ExitCode}");

        if (!string.IsNullOrEmpty(Error))
            sb.AppendLine($"Error: {Error}");

        sb.AppendLine(new string('-', 32));
        return sb.ToString();
    }
}