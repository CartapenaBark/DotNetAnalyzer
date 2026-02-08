using Microsoft.CodeAnalysis;

namespace DotNetAnalyzer.Core.Roslyn;

/// <summary>
/// 性能优化助手 - 提供并行处理和批量操作优化
/// </summary>
public static class PerformanceHelper
{
    /// <summary>
    /// 批量加载项目并并行处理
    /// </summary>
    public static async Task<Project[]> LoadProjectsAsync(WorkspaceManager workspaceManager, IEnumerable<string> projectPaths, int maxDegreeOfParallelism = 4)
    {
        var tasks = projectPaths.Select(async path =>
        {
            try
            {
                return await workspaceManager.GetProjectAsync(path);
            }
            catch
            {
                return null;
            }
        });

        var projects = await Task.WhenAll(tasks);
        return projects.Where(p => p != null).ToArray()!;
    }

    /// <summary>
    /// 并行获取多个项目的诊断信息
    /// </summary>
    public static async Task<Diagnostic[][]> GetDiagnosticsAsync(Project[] projects, int maxDegreeOfParallelism = 4)
    {
        var semaphore = new System.Threading.SemaphoreSlim(maxDegreeOfParallelism);
        var tasks = projects.Select(async project =>
        {
            await semaphore.WaitAsync();
            try
            {
                var compilation = await project.GetCompilationAsync();
                return compilation?.GetDiagnostics().ToArray() ?? Array.Empty<Diagnostic>();
            }
            finally
            {
                semaphore.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 批量分析源文件（用于大型解决方案）
    /// </summary>
    public static async Task<Document[]> LoadDocumentsAsync(Project project, int maxDegreeOfParallelism = 8)
    {
        var documents = project.Documents.ToList();
        if (documents.Count <= 100)
            return documents.ToArray();

        // 对于大型项目，分批加载文档
        var batchSize = 50;
        var batches = (int)Math.Ceiling((double)documents.Count / batchSize);
        var result = new List<Document>();

        for (int i = 0; i < batches; i++)
        {
            var batch = documents.Skip(i * batchSize).Take(batchSize);
            result.AddRange(batch);

            // 每批之间稍微延迟，减少内存压力
            if (i < batches - 1)
                await Task.Delay(10);
        }

        return result.ToArray();
    }

    /// <summary>
    /// 测量操作执行时间
    /// </summary>
    public static async Task<T> MeasureTimeAsync<T>(string operationName, Func<Task<T>> action)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            var result = await action();
            var duration = DateTime.UtcNow - startTime;
            System.Diagnostics.Debug.WriteLine($"{operationName} 完成，耗时: {duration.TotalMilliseconds}ms");
            return result;
        }
        catch
        {
            var duration = DateTime.UtcNow - startTime;
            System.Diagnostics.Debug.WriteLine($"{operationName} 失败，耗时: {duration.TotalMilliseconds}ms");
            throw;
        }
    }

    /// <summary>
    /// 限制并发数量的并行ForEach
    /// </summary>
    public static async Task ParallelForEachAsync<T>(IEnumerable<T> source, Func<T, Task> action, int maxDegreeOfParallelism = 4)
    {
        var semaphore = new System.Threading.SemaphoreSlim(maxDegreeOfParallelism);
        var tasks = source.Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                await action(item);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }
}
