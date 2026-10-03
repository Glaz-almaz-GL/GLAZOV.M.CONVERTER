using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using GLAZOV.M.CONVERTER.Errors;

namespace GLAZOV.M.CONVERTER.Tooling;

/// <summary>
/// Запуск внешней программы: аргументы списком, каналы ввода и вывода, перехват сообщений об ошибках,
/// отмена с завершением процесса вместе с дочерними. Общий механизм всех движков; о форматах ничего не знает.
/// </summary>
internal static class ToolRunner
{
    /// <summary>
    /// Сколько последних строк стандартного потока ошибок хранится в результате.
    /// </summary>
    internal const int KeptErrorLines = 50;

    private const int MaxLineChars = 2000;

    /// <summary>
    /// Запускает программу и ждёт её завершения.
    /// </summary>
    /// <param name="invocation">Что запустить.</param>
    /// <param name="cancellationToken">Отмена: процесс завершается вместе с дочерними, наружу — <see cref="OperationCanceledException"/>.</param>
    /// <exception cref="ConversionException">Программа не запустилась (этап «подготовка», причина «программа не найдена»).</exception>
    internal static async Task<ToolResult> RunAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        cancellationToken.ThrowIfCancellationRequested();

        using Process process = Start(invocation);
        invocation.OnStarted?.Invoke(process.Id);

        Queue<string> errorLines = new();
        Task errors = ReadErrorsAsync(process, errorLines);
        Task input = PumpInputAsync(process, invocation.StandardInput, cancellationToken);
        Task output = PumpOutputAsync(process, invocation.StandardOutput, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(errors, input, output).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            await SettleAsync(process, errors, input, output).ConfigureAwait(false);
            throw;
        }

        string tail;
        lock (errorLines)
        {
            tail = string.Join('\n', errorLines);
        }

        return new ToolResult(process.ExitCode, tail);
    }

    private static Process Start(ToolInvocation invocation)
    {
        ProcessStartInfo info = new(invocation.Path)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardInput = invocation.StandardInput is not null,
            RedirectStandardOutput = invocation.StandardOutput is not null,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (!string.IsNullOrWhiteSpace(invocation.WorkingDirectory))
        {
            info.WorkingDirectory = invocation.WorkingDirectory;
        }

        foreach (string argument in invocation.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        try
        {
            return Process.Start(info) ?? throw new InvalidOperationException("Процесс не создан.");
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException or DirectoryNotFoundException or InvalidOperationException)
        {
            string name = Path.GetFileNameWithoutExtension(invocation.Path);
            throw Failures.ToolNotFound(name, "Проверьте, что программа установлена и путь к ней указан верно.");
        }
    }

    // Читает stderr построчно, всегда: иначе процесс зависнет на заполненном буфере. Хранит только хвост.
    private static async Task ReadErrorsAsync(Process process, Queue<string> lines)
    {
        string? line;
        while ((line = await process.StandardError.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            string kept = line.Length > MaxLineChars ? line[^MaxLineChars..] : line;
            lock (lines)
            {
                lines.Enqueue(kept);
                if (lines.Count > KeptErrorLines)
                {
                    lines.Dequeue();
                }
            }
        }
    }

    // Подаёт вход и закрывает стандартный ввод. Если процесс завершился раньше конца ввода — канал закрыт,
    // это не ошибка запуска: итог скажет код выхода.
    private static async Task PumpInputAsync(Process process, Stream? source, CancellationToken cancellationToken)
    {
        if (source is null)
        {
            return;
        }

        try
        {
            await source.CopyToAsync(process.StandardInput.BaseStream, cancellationToken).ConfigureAwait(false);
            await process.StandardInput.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Процесс закрыл ввод, не дочитав: ожидаемый путь при ошибке программы.
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

    private static async Task PumpOutputAsync(Process process, Stream? destination, CancellationToken cancellationToken)
    {
        if (destination is null)
        {
            return;
        }

        await process.StandardOutput.BaseStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Процесс уже завершился между проверкой и завершением — цель достигнута.
        }
    }

    // После отмены дожидаемся процесса и каналов, не пропуская их собственные сбои отмены.
    private static async Task SettleAsync(Process process, params Task[] pumps)
    {
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        foreach (Task pump in pumps)
        {
            try
            {
                await pump.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
            {
                // Каналы оборваны завершением процесса и отменой — ожидаемо.
            }
        }
    }
}
