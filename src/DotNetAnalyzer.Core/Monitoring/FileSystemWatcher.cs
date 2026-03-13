using System.IO;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Monitoring;

/// <summary>
/// 基于 .NET FileSystemWatcher 的文件监听器实现
/// </summary>
public sealed class FileSystemFileWatcher : IFileWatcher
{
    private readonly ILogger<FileSystemFileWatcher> _logger;
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
    private readonly Dictionary<string, Timer> _debounceTimers = new();
    private readonly int _debounceMilliseconds;

    /// <summary>
    /// 初始化 <see cref="FileSystemFileWatcher"/> 的新实例
    /// </summary>
    public FileSystemFileWatcher(
        ILogger<FileSystemFileWatcher> logger,
        int debounceMilliseconds = 500)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _debounceMilliseconds = debounceMilliseconds;
    }

    /// <inheritdoc />
    public event EventHandler<FileChangeEventArgs>? FileChanged;

    /// <inheritdoc />
    public event EventHandler<ErrorEventArgs>? Error;

    /// <inheritdoc />
    public bool IsWatching => _watchers.Count > 0;

    /// <inheritdoc />
    public IReadOnlyList<string> WatchedPaths => _watchers.Keys.ToList();

    /// <inheritdoc />
    public void StartWatching(string path, string filter = "*.*", bool includeSubdirectories = true)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        if (_watchers.ContainsKey(path))
        {
            _logger.LogWarning("Already watching path: {Path}", path);
            return;
        }

        try
        {
            var directory = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            if (string.IsNullOrEmpty(directory))
            {
                directory = path;
            }

            var watcher = new System.IO.FileSystemWatcher
            {
                Path = directory,
                Filter = filter,
                IncludeSubdirectories = includeSubdirectories,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
            };

            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Deleted += OnFileChanged;
            watcher.Renamed += OnFileRenamed;
            watcher.Error += OnError;

            watcher.EnableRaisingEvents = true;
            _watchers[path] = watcher;

            _logger.LogInformation("Started watching path: {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start watching path: {Path}", path);
            Error?.Invoke(this, new ErrorEventArgs
            {
                Message = $"Failed to start watching path: {path}",
                Exception = ex
            });
        }
    }

    /// <inheritdoc />
    public void StopWatching()
    {
        foreach (var watcher in _watchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        foreach (var timer in _debounceTimers.Values)
        {
            timer.Dispose();
        }

        _watchers.Clear();
        _debounceTimers.Clear();

        _logger.LogInformation("Stopped all file watching");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        StopWatching();
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var changeType = e.ChangeType switch
        {
            System.IO.WatcherChangeTypes.Created => FileChangeType.Created,
            System.IO.WatcherChangeTypes.Changed => FileChangeType.Changed,
            System.IO.WatcherChangeTypes.Deleted => FileChangeType.Deleted,
            _ => FileChangeType.Changed
        };

        DebounceEvent(e.FullPath, changeType, oldPath: null);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        DebounceEvent(e.FullPath, FileChangeType.Renamed, e.OldFullPath);
    }

    private void OnError(object sender, System.IO.ErrorEventArgs e)
    {
        var exception = e.GetException();
        _logger.LogError(exception, "File watcher error occurred");

        Error?.Invoke(this, new ErrorEventArgs
        {
            Message = "File watcher error occurred",
            Exception = exception
        });
    }

    private void DebounceEvent(string fullPath, FileChangeType changeType, string? oldPath)
    {
        // 取消之前的定时器
        if (_debounceTimers.TryGetValue(fullPath, out var existingTimer))
        {
            existingTimer.Dispose();
            _debounceTimers.Remove(fullPath);
        }

        // 创建新的防抖定时器
        var timer = new Timer(_ =>
        {
            try
            {
                var args = new FileChangeEventArgs
                {
                    FullPath = fullPath,
                    ChangeType = changeType,
                    OldPath = oldPath
                };

                FileChanged?.Invoke(this, args);

                _logger.LogDebug("File changed event raised: {Path}, Type: {Type}",
                    fullPath, changeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising file changed event");
            }
            finally
            {
                _debounceTimers.Remove(fullPath);
            }
        }, null, _debounceMilliseconds, Timeout.Infinite);

        _debounceTimers[fullPath] = timer;
    }
}

/// <summary>
/// 防抖文件监听器
/// </summary>
/// <remarks>
/// 提供防抖机制，避免短时间内多次触发事件。
/// </remarks>
public sealed class DebouncedFileWatcher : IFileWatcher
{
    private readonly IFileWatcher _innerWatcher;
    private readonly int _debounceMilliseconds;
    private readonly Dictionary<string, Timer> _pendingChanges = new();
    private readonly object _lock = new();

    /// <summary>
    /// 初始化 <see cref="DebouncedFileWatcher"/> 的新实例
    /// </summary>
    public DebouncedFileWatcher(IFileWatcher innerWatcher, int debounceMilliseconds = 500)
    {
        _innerWatcher = innerWatcher ?? throw new ArgumentNullException(nameof(innerWatcher));
        _debounceMilliseconds = debounceMilliseconds;

        _innerWatcher.FileChanged += OnInnerFileChanged;
        _innerWatcher.Error += (s, e) => Error?.Invoke(s, e);
    }

    /// <inheritdoc />
    public event EventHandler<FileChangeEventArgs>? FileChanged;

    /// <inheritdoc />
    public event EventHandler<ErrorEventArgs>? Error;

    /// <inheritdoc />
    public bool IsWatching => _innerWatcher.IsWatching;

    /// <inheritdoc />
    public IReadOnlyList<string> WatchedPaths => _innerWatcher.WatchedPaths;

    /// <inheritdoc />
    public void StartWatching(string path, string filter = "*.*", bool includeSubdirectories = true)
    {
        _innerWatcher.StartWatching(path, filter, includeSubdirectories);
    }

    /// <inheritdoc />
    public void StopWatching()
    {
        _innerWatcher.StopWatching();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _innerWatcher.Dispose();

        lock (_lock)
        {
            foreach (var timer in _pendingChanges.Values)
            {
                timer.Dispose();
            }

            _pendingChanges.Clear();
        }
    }

    private void OnInnerFileChanged(object? sender, FileChangeEventArgs e)
    {
        lock (_lock)
        {
            // 取消之前的定时器
            if (_pendingChanges.TryGetValue(e.FullPath, out var existingTimer))
            {
                existingTimer.Dispose();
                _pendingChanges.Remove(e.FullPath);
            }

            // 创建新的定时器
            var timer = new Timer(_ =>
            {
                FileChanged?.Invoke(this, e);

                lock (_lock)
                {
                    _pendingChanges.Remove(e.FullPath);
                }
            }, null, _debounceMilliseconds, Timeout.Infinite);

            _pendingChanges[e.FullPath] = timer;
        }
    }
}
