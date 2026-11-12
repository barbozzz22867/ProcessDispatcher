using ProcessDispatcher.Services;

namespace ProcessDispatcher;

internal class Program
{
    private static async Task Main()
    {
        string logPath = Path.Combine(AppContext.BaseDirectory, "process-log.txt");
        var logger = new ProcessLogger(logPath);
        var runner = new ProcessRunner(logger);

        Console.WriteLine("=== PROCESS DISPATCHER ===");
        Console.WriteLine($"Журнал: {logPath}");
        Console.WriteLine();

        while (true)
        {
            Console.Write("Программа (например, dotnet / ping / exit для выхода): ");
            string? program = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(program) || program.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            Console.Write("Аргументы: ");
            string arguments = Console.ReadLine() ?? "";

            Console.Write("Тайм-аут, сек (Enter — 10): ");
            string? t = Console.ReadLine();
            int seconds = int.TryParse(t, out int v) && v > 0 ? v : 10;

            using var cts = new CancellationTokenSource();
            Console.WriteLine("\nНажмите Ctrl+C для отмены...");
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            Console.WriteLine($"[Запуск] {program} {arguments}");

            var entry = await runner.RunAsync(
                program,
                arguments,
                TimeSpan.FromSeconds(seconds),
                cts.Token);

            Console.WriteLine();
            Console.WriteLine($"PID: {entry.Pid?.ToString() ?? "—"}");
            Console.WriteLine($"Длительность: {entry.Duration:hh\\:mm\\:ss\\.fff}");
            Console.WriteLine($"Код завершения: {entry.ExitCode?.ToString() ?? "—"}");
            if (!string.IsNullOrEmpty(entry.Error))
                Console.WriteLine($"Ошибка: {entry.Error}");

            if (!string.IsNullOrEmpty(runner.LastStdout))
            {
                Console.WriteLine("\n--- stdout ---");
                Console.WriteLine(runner.LastStdout);
            }
            if (!string.IsNullOrEmpty(runner.LastStderr))
            {
                Console.WriteLine("\n--- stderr ---");
                Console.WriteLine(runner.LastStderr);
            }

            Console.WriteLine("\nЗапись добавлена в журнал.");
            Console.WriteLine();
        }

        Console.WriteLine("Работа завершена.");
    }
}