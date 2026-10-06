using System.Diagnostics;

namespace GLAZOV.M.CONVERTER.Office.Tests.Live;

/// <summary>
/// Считает, сколько процессов с заданным именем (по умолчанию FFmpeg) одновременно работает и сколько памяти они занимают,
/// опрашивая систему каждые несколько миллисекунд.
/// </summary>
public sealed class PeakWatcher : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    private int _peak;
    private long _peakMemory;

    public PeakWatcher(string processName = "ffmpeg")
    {
        _loop = Task.Run(async () =>
        {
            while (!_stop.IsCancellationRequested)
            {
                Process[] now = Process.GetProcessesByName(processName);
                int count = now.Length;
                long memory = 0;
                foreach (Process process in now)
                {
                    try
                    {
                        memory += process.WorkingSet64;
                    }
                    catch (InvalidOperationException)
                    {
                        // The process ended between listing and reading.
                    }

                    process.Dispose();
                }

                long seenMemory;
                while (memory > (seenMemory = Interlocked.Read(ref _peakMemory)))
                {
                    Interlocked.CompareExchange(ref _peakMemory, memory, seenMemory);
                }

                int seen;
                while (count > (seen = Volatile.Read(ref _peak)))
                {
                    Interlocked.CompareExchange(ref _peak, count, seen);
                }

                await Task.Delay(3);
            }
        });
    }

    public int Peak => Volatile.Read(ref _peak);

    /// <summary>The most memory the watched processes held together, in bytes.</summary>
    public long PeakMemory => Interlocked.Read(ref _peakMemory);

    public void Dispose()
    {
        _stop.Cancel();
        _loop.Wait();
        _stop.Dispose();
    }
}
