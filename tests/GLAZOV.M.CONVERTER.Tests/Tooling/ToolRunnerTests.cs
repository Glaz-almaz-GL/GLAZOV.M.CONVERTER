using System.Diagnostics;
using System.Text;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests.Tooling;

/// <summary>
/// Запуск внешней программы на настоящих системных программах: код выхода, stderr, каналы ввода и вывода,
/// отмена, зависание, отсутствие программы. Для каждой системы — свой набор программ.
/// </summary>
public class ToolRunnerTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    // Оболочка системы: на Windows cmd, на остальных sh.
    private static ToolInvocation Shell(string script, Stream? input = null, Stream? output = null, Action<int>? onStarted = null)
    {
        return OperatingSystem.IsWindows()
            ? new ToolInvocation("cmd.exe", ["/d", "/c", script], input, output, OnStarted: onStarted)
            : new ToolInvocation("/bin/sh", ["-c", script], input, output, OnStarted: onStarted);
    }

    private static string SortProgram()
    {
        return OperatingSystem.IsWindows() ? Path.Combine(Environment.SystemDirectory, "sort.exe") : "/usr/bin/sort";
    }

    [Fact]
    public async Task ExitCodeZero_IsReturnedAsResult()
    {
        ToolResult result = await ToolRunner.RunAsync(Shell("exit 0")).WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.ErrorOutput);
    }

    [Fact]
    public async Task NonZeroExit_IsNotAnException_AndStderrIsCaptured()
    {
        string script = OperatingSystem.IsWindows() ? "echo error one 1>&2 & exit 3" : "echo 'error one' >&2; exit 3";

        ToolResult result = await ToolRunner.RunAsync(Shell(script)).WaitAsync(Patience);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("error one", result.ErrorOutput);
    }

    [Fact]
    public async Task StdinToStdout_ThroughPipes()
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes("яблоко\n"));
        using MemoryStream output = new();

        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(SortProgram(), [], input, output)).WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("яблоко", Encoding.UTF8.GetString(output.ToArray()).Trim());
    }

    [Fact]
    public async Task LargeInput_GoesThroughPipesWithoutDeadlock()
    {
        byte[] line = Encoding.ASCII.GetBytes("0123456789012345678901234567890123456789012345678901234567890123456789\n");
        using MemoryStream input = new();
        for (int i = 0; i < 20_000; i++)
        {
            input.Write(line);
        }

        input.Position = 0;
        using MemoryStream output = new();

        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(SortProgram(), [], input, output)).WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(20_000, Encoding.ASCII.GetString(output.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task HugeStderr_DoesNotHangTheProcess_AndOnlyTheTailIsKept()
    {
        string script = OperatingSystem.IsWindows()
            ? "for /L %i in (1,1,5000) do @echo line %i 1>&2"
            : "i=1; while [ $i -le 5000 ]; do echo line $i >&2; i=$((i+1)); done";

        ToolResult result = await ToolRunner.RunAsync(Shell(script)).WaitAsync(Patience);

        string[] lines = result.ErrorOutput.Split('\n');
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(ToolRunner.KeptErrorLines, lines.Length);
        Assert.Equal("line 5000", lines[^1].Trim());
    }

    [Fact]
    public async Task Cancel_KillsTheProcessTree_AndThrowsOperationCanceled()
    {
        string script = OperatingSystem.IsWindows() ? "ping -n 60 127.0.0.1 >nul" : "sleep 60";
        int pid = 0;
        using CancellationTokenSource cts = new();
        Task<ToolResult> run = ToolRunner.RunAsync(Shell(script, onStarted: id => pid = id), cts.Token);

        await Task.Delay(500);
        Stopwatch watch = Stopwatch.StartNew();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await run.WaitAsync(Patience));
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), "Отмена не должна ждать конца долгой программы.");
        Assert.NotEqual(0, pid);
        Assert.False(IsRunning(pid), "Процесс остался после отмены.");
    }

    [Fact]
    public async Task CancelledBeforeStart_NothingIsStarted()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        int pid = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await ToolRunner.RunAsync(Shell("exit 0", onStarted: id => pid = id), cts.Token));

        Assert.Equal(0, pid);
    }

    [Fact]
    public async Task MissingProgram_IsToolNotFoundAtPreparation_NotAWin32Exception()
    {
        string missing = Path.Combine(Path.GetTempPath(), "нет-такой-программы-" + Guid.NewGuid().ToString("N"));

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await ToolRunner.RunAsync(new ToolInvocation(missing, [])));

        Assert.Equal(ConversionStage.Preparation, ex.Stage);
        Assert.Equal(ConversionFailureReason.ToolNotFound, ex.Reason);
        Assert.Contains("нет-такой-программы-", ex.Message);
    }

    [Fact]
    public async Task ArgumentsAreAList_SpacesAndQuotesNeedNoEscaping()
    {
        // Одна строка с пробелами и кавычкой доходит до программы целиком одним аргументом.
        const string tricky = "one two \"three\"";
        using MemoryStream output = new();
        string script = Path.Combine(Path.GetTempPath(), "convtests-arg-" + Guid.NewGuid().ToString("N") + ".ps1");
        try
        {
            ToolInvocation invocation;
            if (OperatingSystem.IsWindows())
            {
                await File.WriteAllTextAsync(script, "param([string]$a) [Console]::Out.Write($a)");
                invocation = new ToolInvocation("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, tricky], null, output);
            }
            else
            {
                invocation = new ToolInvocation("/usr/bin/printf", ["%s", tricky], null, output);
            }

            ToolResult result = await ToolRunner.RunAsync(invocation).WaitAsync(Patience);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(tricky, Encoding.UTF8.GetString(output.ToArray()));
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Fact]
    public async Task ProgramThatExitsWithoutReadingInput_IsNotAnErrorOfTheRunner()
    {
        using MemoryStream input = new(new byte[5_000_000]);

        ToolResult result = await ToolRunner.RunAsync(Shell("exit 7", input)).WaitAsync(Patience);

        Assert.Equal(7, result.ExitCode);
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
