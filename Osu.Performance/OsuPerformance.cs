using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Osu.Performance;

/// <summary>
///     Wraps the osu-native gradual performance calculator on a background thread,
///     recalculating live PP as the game reports score judgements.
/// </summary>
[UsedImplicitly]
public class OsuPerformance : IDisposable
{
    private readonly ConcurrentQueue<PendingUpdate> _queue = new();
    private readonly CancellationTokenSource _cancellation;
    private readonly string _mapPath;
    private readonly uint _mods;
    private readonly int _mode;

    private OsuGradualPerformance? _gradual;
    private volatile bool _disposed;
    private volatile bool _failed;

    [UsedImplicitly]
    public event Action<double>? OnNewCalculation;

    public OsuPerformance(int mode, string mapPath, uint mods)
    {
        _mode = mode;
        _mapPath = mapPath;
        _mods = mods;
        _cancellation = new CancellationTokenSource();

        Debug.WriteLine("Starting background task", nameof(OsuPerformance));
        Task.Factory.StartNew(
            InitializeAndProcessQueue,
            _cancellation.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
    }

    public void Dispose()
    {
        _disposed = true;
        _cancellation.Cancel();
        _gradual?.Dispose();
        _gradual = null;
        OnNewCalculation = null;
    }

    /// <summary>
    ///     Queues the current score state to recalculate performance at the given playhead time.
    /// </summary>
    /// <param name="timeMs">The time of the most recently judged object.</param>
    /// <param name="score">The current score state.</param>
    [UsedImplicitly]
    public void AddScoreUpdate(int timeMs, PerformanceScore score)
    {
        if (_disposed || _failed || _cancellation.IsCancellationRequested)
            return;

        _queue.Enqueue(new PendingUpdate
        {
            TimeMs = timeMs,
            Score = score,
        });
    }

    private void InitializeAndProcessQueue()
    {
        try
        {
            OsuNativeCalls.EnsureLoaded();

            var beatmapText = File.ReadAllBytes(_mapPath);
            _gradual = new OsuGradualPerformance(_mode, beatmapText, _mods);

            if (_disposed)
            {
                _gradual.Dispose();
                _gradual = null;
                return;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to initialize osu-native performance calculator: {e}");
            _failed = true;
            return;
        }

        ProcessQueue();
    }

    private async void ProcessQueue()
    {
        while (!_disposed && !_cancellation.IsCancellationRequested)
        {
            var gradual = _gradual;
            if (gradual == null)
            {
                await Task.Delay(100);
                continue;
            }

            while (_queue.TryDequeue(out var item))
            {
                if (gradual.AdvanceAtTime(item.TimeMs, item.Score, out var performance))
                    OnNewCalculation?.Invoke(performance);

                if (_disposed || _cancellation.IsCancellationRequested)
                    return;
            }

            await Task.Delay(100);
        }
    }

    private struct PendingUpdate
    {
        public int TimeMs;
        public PerformanceScore Score;
    }
}
