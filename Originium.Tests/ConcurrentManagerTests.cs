using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Originium.Services;

namespace Originium.Tests;

public class ConcurrentManagerTests : IDisposable
{
    private readonly Mock<ILogger<ConcurrentManager>> _loggerMock;
    private ConcurrentManager _manager;

    public ConcurrentManagerTests()
    {
        _loggerMock = new Mock<ILogger<ConcurrentManager>>();
        _manager = new ConcurrentManager(maxConcurrentTasks: 2, _loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithZeroConcurrency_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConcurrentManager(0));
    }

    [Fact]
    public void Constructor_WithNegativeConcurrency_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConcurrentManager(-1));
    }

    [Fact]
    public void Constructor_WithValidConcurrency_SetsProperties()
    {
        var manager = new ConcurrentManager(5);
        Assert.Equal(5, manager.MaxConcurrency);
        Assert.Equal(0, manager.RunningCount);
        Assert.Equal(0, manager.PendingCount);
    }

    [Fact]
    public async Task EnqueueAsync_WithConcurrencyLimit_RespectsLimit()
    {
        var runningCount = 0;
        var maxRunning = 0;
        var lockObj = new object();

        var tasks = new List<Task>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(_manager.EnqueueAsync(async token =>
            {
                lock (lockObj)
                {
                    runningCount++;
                    maxRunning = Math.Max(maxRunning, runningCount);
                }
                await Task.Delay(10, token);
                lock (lockObj)
                {
                    runningCount--;
                }
                return 42;
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(2, maxRunning);
    }

    [Fact]
    public async Task EnqueueAsync_ReturnsCorrectResult()
    {
        var result = await _manager.EnqueueAsync(async _ =>
        {
            await Task.Delay(1);
            return "test result";
        });

        Assert.Equal("test result", result);
    }

    [Fact]
    public async Task EnqueueAsync_MultipleTasks_ResultsAreCorrect()
    {
        var tasks = new List<Task<int>>();
        for (int i = 0; i < 10; i++)
        {
            int value = i;
            tasks.Add(_manager.EnqueueAsync(async _ =>
            {
                await Task.Delay(1);
                return value * 2;
            }));
        }

        var results = await Task.WhenAll(tasks);

        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(i * 2, results[i]);
        }
    }

    [Fact]
    public async Task EnqueueAsync_WithCancellation_TaskIsCancelled()
    {
        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource<bool>();

        var task = _manager.EnqueueAsync(async token =>
        {
            tcs.SetResult(true);
            await Task.Delay(5000, token);
            return 42;
        }, cts.Token);

        await tcs.Task;
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() => task);
    }

    [Fact]
    public async Task EnqueueAsync_WithNullFunc_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _manager.EnqueueAsync((Func<CancellationToken, Task>)null!));
    }

    [Fact]
    public async Task EnqueueAsync_NoReturnValue_WorksCorrectly()
    {
        var wasExecuted = false;

        await _manager.EnqueueAsync(async token =>
        {
            await Task.Delay(1, token);
            wasExecuted = true;
        });

        Assert.True(wasExecuted);
    }

    [Fact]
    public async Task EnqueueAsync_NoReturnValueWithResult_WorksCorrectly()
    {
        var result = await _manager.EnqueueAsync(async () =>
        {
            await Task.Delay(1);
            return "hello";
        });

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task EnqueueAsync_NoReturnValueNoToken_WorksCorrectly()
    {
        var wasExecuted = false;

        await _manager.EnqueueAsync(async () =>
        {
            await Task.Delay(1);
            wasExecuted = true;
        });

        Assert.True(wasExecuted);
    }

    [Fact]
    public async Task EnqueueAsync_Exception_IsPropagated()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.EnqueueAsync(async _ =>
            {
                await Task.Delay(1);
                throw new InvalidOperationException("test error");
            }));

        Assert.Equal("test error", exception.Message);
    }

    [Fact]
    public async Task CancelAll_PendingTasksAreCancelled()
    {
        var manager = new ConcurrentManager(1);

        var firstTask = manager.EnqueueAsync(async token =>
        {
            await Task.Delay(5000, token);
            return 1;
        });

        var pendingTask = manager.EnqueueAsync(async token =>
        {
            await Task.Delay(5000, token);
            return 2;
        });

        manager.CancelAll();

        await Assert.ThrowsAsync<OperationCanceledException>(() => pendingTask);
    }

    [Fact]
    public async Task RunningCount_IncreasesWithExecution()
    {
        using var manager = new ConcurrentManager(3);
        var tcs = new TaskCompletionSource<bool>();

        var task1 = manager.EnqueueAsync(async _ =>
        {
            tcs.SetResult(true);
            await Task.Delay(10);
            return 1;
        });

        await tcs.Task;
        Assert.Equal(1, manager.RunningCount);
        await task1;
    }

    [Fact]
    public async Task PendingCount_IncreasesWhenQueueing()
    {
        using var manager = new ConcurrentManager(1);
        var tcs1 = new TaskCompletionSource<bool>();

        var task1 = manager.EnqueueAsync(async _ =>
        {
            tcs1.SetResult(true);
            await Task.Delay(10);
            return 1;
        });

        await tcs1.Task;
        Assert.Equal(1, manager.RunningCount);

        var task2 = manager.EnqueueAsync(async _ =>
        {
            await Task.Delay(10);
            return 2;
        });

        Assert.Equal(1, manager.PendingCount);
        await task1;
        await task2;
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }
}
