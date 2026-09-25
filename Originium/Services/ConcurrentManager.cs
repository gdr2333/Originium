using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Originium.Services;

/// <summary>
/// 限制并发数量的任务执行器。
/// 超出最大并发数的任务会自动排队，有空闲位置时再执行。
/// </summary>
public class ConcurrentManager : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly CancellationTokenSource _cancelAllCts = new CancellationTokenSource();
    private int _pendingCount;
    private readonly ILogger<ConcurrentManager>? logger;

    /// <summary>最大并发任务数。</summary>
    public int MaxConcurrency { get; }

    /// <summary>当前正在执行的任务数。</summary>
    public int RunningCount => MaxConcurrency - _semaphore.CurrentCount;

    /// <summary>当前排队等待的任务数。</summary>
    public int PendingCount => Volatile.Read(ref _pendingCount);

    public ConcurrentManager(int maxConcurrentTasks, ILogger<ConcurrentManager> logger = null)
    {
        if (maxConcurrentTasks < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentTasks), "最大并发数必须大于等于 1。");

        this.logger = logger;
        MaxConcurrency = maxConcurrentTasks;
        _semaphore = new SemaphoreSlim(maxConcurrentTasks, maxConcurrentTasks);
        this.logger?.LogDebug("ConcurrentManager 初始化，最大并发数: {MaxConcurrency}", maxConcurrentTasks);
    }

    /// <summary>
    /// 添加一个任务。未达到并发上限时立即执行，否则排队等待。
    /// </summary>
    /// <param name="func">返回 Task&lt;T&gt; 的工作函数，参数为可用于协作式取消的令牌。</param>
    /// <param name="cancellationToken">
    /// 取消令牌：任务还在排队时取消，则任务不会执行；
    /// 任务已开始运行，则令牌传给 func，由任务内部响应取消。
    /// </param>
    /// <returns>在原任务结束后完成、并携带其结果的 Task。</returns>
    public async Task<T> EnqueueAsync<T>(
        Func<CancellationToken, Task<T>> func,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                   cancellationToken, _cancelAllCts.Token);
        Interlocked.Increment(ref _pendingCount);
        logger?.LogDebug("任务入队，当前排队数: {PendingCount}, 运行中: {RunningCount}",
            PendingCount, RunningCount);

        try
        {
            await _semaphore.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            logger?.LogDebug("任务开始执行，运行中: {RunningCount}, 排队数: {PendingCount}",
                RunningCount, PendingCount);
        }
        catch (OperationCanceledException)
        {
            Interlocked.Decrement(ref _pendingCount);
            logger?.LogDebug("任务在排队等待时被取消");
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _pendingCount);
        }

        try
        {
            return await func(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            logger?.LogDebug("任务执行过程中被取消");
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "任务执行过程中发生错误");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // ---------- 便捷重载 ----------

    /// <summary>无返回值版本（工作函数接收取消令牌）。</summary>
    public Task EnqueueAsync(Func<CancellationToken, Task> func,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);
        return EnqueueAsync<object?>(async token =>
        {
            await func(token).ConfigureAwait(false);
            return null;
        }, cancellationToken);
    }

    /// <summary>带返回值、但工作函数不需要令牌的版本。</summary>
    public Task<T> EnqueueAsync<T>(Func<Task<T>> func,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);
        return EnqueueAsync(_ => func(), cancellationToken);
    }

    /// <summary>无返回值、工作函数也不需要令牌的版本。</summary>
    public Task EnqueueAsync(Func<Task> func,
        CancellationToken cancellationToken = default)
    {
        if (func == null) throw new ArgumentNullException(nameof(func));
        return EnqueueAsync<object?>(async _ =>
        {
            await func().ConfigureAwait(false);
            return null;
        }, cancellationToken);
    }

    /// <summary>取消所有任务：排队中的任务不再执行；运行中的任务收到取消信号（需内部配合）。</summary>
    public void CancelAll()
    {
        logger?.LogWarning("取消所有任务");
        _cancelAllCts.Cancel();
    }

    public void Dispose()
    {
        logger?.LogDebug("ConcurrentManager 正在释放资源");
        _cancelAllCts.Cancel();
        _cancelAllCts.Dispose();
        _semaphore.Dispose();
    }
}
