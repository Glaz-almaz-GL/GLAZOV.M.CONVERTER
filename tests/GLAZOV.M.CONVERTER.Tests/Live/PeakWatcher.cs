using System.Diagnostics;

namespace GLAZOV.M.CONVERTER.Tests.Live;

/// <summary>
/// Считает, сколько процессов FFmpeg одновременно работает, опрашивая систему каждые несколько миллисекунд.
/// </summary>
public sealed class PeakWatcher : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    private int _peak;

    public PeakWatcher()
    {
        _loop = Task.Run(async () =>
        {
            while (!_stop.IsCancellationRequested)
            {
                Process[] now = Process.GetProcessesByName("ffmpeg");
                int count = now.Length;
                foreach (Process process in now)
                {
                    process.Dispose();
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

    public void Dispose()
    {
        _stop.Cancel();
        _loop.Wait();
        _stop.Dispose();
    }
}
