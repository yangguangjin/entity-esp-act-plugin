using System;
using System.Threading;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

/// <summary>功能：把 VFX 重型内存采样放到后台队列，UI 刷新只读取上一帧缓存，避免 ACT/WinForms UI 被 Scene.World 遍历阻塞。</summary>
public sealed class VfxSnapshotSampler
{
    /// <summary>功能：限制默认 active VFX 内存采样频率，和文本 UI/overlay 渲染 FPS 解耦。</summary>
    public const int DefaultSampleHz = 10;

    /// <summary>功能：限制用户配置上限，避免把内存遍历重新调成每帧扫描。</summary>
    public const int MaxSampleHz = 30;

    /// <summary>功能：保护缓存快照、采样状态和状态文本。</summary>
    private readonly object _syncRoot = new object();

    /// <summary>功能：执行真正的重型 VFX 采样；生产环境中会遍历 FF14 Scene.World。</summary>
    private readonly Func<EspConfig, VfxMonitorSnapshot> _sample;

    /// <summary>功能：把采样任务投递到后台；测试中可替换为捕获队列来证明 UI 不同步执行采样。</summary>
    private readonly Action<Action> _queueWork;

    /// <summary>功能：提供可测试的时间源，用于采样间隔判断。</summary>
    private readonly Func<DateTime> _clock;

    /// <summary>功能：保存最近一次完成的快照，供 UI tick 直接读取。</summary>
    private VfxMonitorSnapshot _latest = VfxMonitorSnapshot.Empty;

    /// <summary>功能：标记后台采样已排队或正在运行，避免 UI 高频 tick 重复投递相同工作。</summary>
    private bool _isSampling;

    /// <summary>功能：记录最近一次采样开始时间，用于按 VfxSampleHz 节流。</summary>
    private DateTime _lastSampleStartedAt = DateTime.MinValue;

    /// <summary>功能：记录最近一次采样完成时间，用于状态栏展示采样新鲜度。</summary>
    private DateTime _lastSampleCompletedAt = DateTime.MinValue;

    /// <summary>功能：给 VFX 面板状态栏展示后台采样状态和错误。</summary>
    private string _status = "VFX sampler: waiting for first sample";

    public VfxSnapshotSampler(Func<EspConfig, VfxMonitorSnapshot> sample)
        : this(sample, action => ThreadPool.QueueUserWorkItem(_ => action()), () => DateTime.UtcNow)
    {
    }

    public VfxSnapshotSampler(Func<EspConfig, VfxMonitorSnapshot> sample, Action<Action> queueWork, Func<DateTime> clock)
    {
        _sample = sample ?? throw new ArgumentNullException(nameof(sample));
        _queueWork = queueWork ?? throw new ArgumentNullException(nameof(queueWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public string StatusText
    {
        get
        {
            lock (_syncRoot)
            {
                return _status;
            }
        }
    }

    /// <summary>功能：返回当前缓存快照，并在采样间隔到期时只排队一个后台采样任务。</summary>
    public VfxMonitorSnapshot GetLatest(EspConfig config)
    {
        config ??= new EspConfig();
        Action? queuedWork = null;
        VfxMonitorSnapshot latest;
        var now = _clock();
        lock (_syncRoot)
        {
            if (!_isSampling && IsSampleDue(now, config))
            {
                var capturedConfig = CopyVfxConfig(config);
                _isSampling = true;
                _lastSampleStartedAt = now;
                _status = "VFX sampler: background sample queued";
                queuedWork = () => RunSample(capturedConfig);
            }

            latest = _latest;
        }

        if (queuedWork != null)
        {
            QueueSampleWork(queuedWork);
        }

        return latest;
    }

    /// <summary>功能：清空采样缓存，通常在 VFX 面板关闭或 FF14 进程重连时调用。</summary>
    public void Reset()
    {
        lock (_syncRoot)
        {
            _latest = VfxMonitorSnapshot.Empty;
            _isSampling = false;
            _lastSampleStartedAt = DateTime.MinValue;
            _lastSampleCompletedAt = DateTime.MinValue;
            _status = "VFX sampler: waiting for first sample";
        }
    }

    /// <summary>功能：规范化采样 Hz，防止 0、负数或过大值导致忙等扫描。</summary>
    public static int NormalizeSampleHz(EspConfig config)
    {
        var configured = config?.VfxSampleHz ?? DefaultSampleHz;
        return Math.Max(1, Math.Min(MaxSampleHz, configured));
    }

    /// <summary>功能：把 VFX 采样 Hz 转成后台采样最小间隔。</summary>
    public static TimeSpan GetSampleInterval(EspConfig config)
    {
        return TimeSpan.FromMilliseconds(Math.Max(1, 1000 / NormalizeSampleHz(config)));
    }

    /// <summary>功能：判断当前 UI tick 是否已经到达下一次后台采样时间。</summary>
    private bool IsSampleDue(DateTime now, EspConfig config)
    {
        return _lastSampleStartedAt == DateTime.MinValue || now - _lastSampleStartedAt >= GetSampleInterval(config);
    }

    /// <summary>功能：安全投递后台采样；如果队列失败，恢复采样状态并把错误显示到状态栏。</summary>
    private void QueueSampleWork(Action queuedWork)
    {
        try
        {
            _queueWork(queuedWork);
        }
        catch (Exception ex)
        {
            lock (_syncRoot)
            {
                _isSampling = false;
                _status = "VFX sampler: queue failed: " + ex.Message;
            }
        }
    }

    /// <summary>功能：在后台执行重型采样，并把结果发布为下一次 UI tick 可读取的缓存。</summary>
    private void RunSample(EspConfig capturedConfig)
    {
        try
        {
            var snapshot = _sample(capturedConfig) ?? VfxMonitorSnapshot.Empty;
            var completedAt = _clock();
            lock (_syncRoot)
            {
                _latest = snapshot;
                _lastSampleCompletedAt = completedAt;
                _isSampling = false;
                _status = "VFX sampler: sample ok live=" + snapshot.LiveEntries.Count
                    + " history=" + snapshot.HistoryEntries.Count
                    + " at=" + _lastSampleCompletedAt.ToString("HH:mm:ss.fff");
            }
        }
        catch (Exception ex)
        {
            lock (_syncRoot)
            {
                _isSampling = false;
                _status = "VFX sampler: sample failed: " + ex.Message;
            }
        }
    }

    /// <summary>功能：复制后台采样所需的 VFX 配置字段，避免 UI 线程编辑配置时后台读到半更新对象。</summary>
    private static EspConfig CopyVfxConfig(EspConfig config)
    {
        return new EspConfig
        {
            VfxMaxDistance = config.VfxMaxDistance,
            VfxDisplaySeconds = config.VfxDisplaySeconds,
            VfxMaxRows = config.VfxMaxRows,
            VfxShortLivedMaxAgeSeconds = config.VfxShortLivedMaxAgeSeconds,
            VfxShortLivedHoldSeconds = config.VfxShortLivedHoldSeconds,
            VfxSampleHz = config.VfxSampleHz,
        };
    }
}
