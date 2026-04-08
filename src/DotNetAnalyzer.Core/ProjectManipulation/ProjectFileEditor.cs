using System.Diagnostics;
using DotNetAnalyzer.Core.ProjectManipulation.Models;
using DotNetAnalyzer.Core.Security;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.ProjectManipulation;

/// <summary>
/// 提供对 .csproj 项目文件的编辑能力，包括添加/移除引用、修改包版本和 MSBuild 属性。
/// </summary>
/// <remarks>
/// 所有编辑操作在执行前会自动备份原始文件（<c>.bak</c> 后缀），
/// 并通过 <see cref="PathValidator.ValidateProjectPath"/> 验证路径安全性。
/// <para>编辑基于 <see cref="Project"/> API，可正确处理条件、
/// 多目标框架和属性表继承等 MSBuild 语义。</para>
/// </remarks>
public sealed class ProjectFileEditor
{
    private static readonly Action<ILogger, string, string, Exception?> s_logAddProjectRef =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(1, nameof(AddProjectReference)),
            "添加项目引用: {ProjectPath} <- {ReferencePath}");

    private static readonly Action<ILogger, string, string, Exception?> s_logRemoveProjectRef =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2, nameof(RemoveProjectReference)),
            "移除项目引用: {ProjectPath} <- {ReferencePath}");

    private static readonly Action<ILogger, string, string, string, Exception?> s_logAddPackageRef =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(3, nameof(AddPackageReference)),
            "添加包引用: {ProjectPath} <- {PackageId}@{Version}");

    private static readonly Action<ILogger, string, string, string, Exception?> s_logUpdatePackageVer =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(4, nameof(UpdatePackageVersion)),
            "更新包版本: {ProjectPath} {PackageId} -> {NewVersion}");

    private static readonly Action<ILogger, string, string, string, Exception?> s_logModifyProperty =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(5, nameof(ModifyProperty)),
            "修改属性: {ProjectPath} {PropertyName} = {Value}");

    private static readonly Action<ILogger, string, string, double, Exception?> s_logEditComplete =
        LoggerMessage.Define<string, string, double>(
            LogLevel.Information,
            new EventId(6, "EditComplete"),
            "编辑完成: {ProjectPath} 操作={Operation}, 耗时={ElapsedMs:F1}ms");

    private static readonly Action<ILogger, string, string, Exception?> s_logError =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(7, "EditError"),
            "编辑项目文件失败: {ProjectPath} 操作={Operation}");

    private readonly ILogger<ProjectFileEditor> _logger;

    /// <summary>
    /// 初始化 <see cref="ProjectFileEditor"/> 的新实例。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    public ProjectFileEditor(ILogger<ProjectFileEditor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 向项目文件添加 ProjectReference 引用。
    /// </summary>
    /// <param name="projectPath">目标项目文件路径（.csproj）。</param>
    /// <param name="referencePath">要引用的项目文件路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>编辑操作结果。</returns>
    public async Task<ProjectEditResult> AddProjectReference(
        string projectPath,
        string referencePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        ArgumentException.ThrowIfNullOrEmpty(referencePath);

        return await ExecuteEditAsync(
            projectPath,
            "AddProjectReference",
            project =>
            {
                var relativeRef = GetRelativePath(projectPath, referencePath);
                s_logAddProjectRef(_logger, projectPath, relativeRef, null);

                // 检查是否已存在相同引用
                var existing = project.GetItems("ProjectReference")
                    .FirstOrDefault(item => string.Equals(
                        item.EvaluatedInclude,
                        relativeRef,
                        StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    throw new InvalidOperationException(
                        $"Project reference already exists: {relativeRef}");
                }

                project.AddItem("ProjectReference", relativeRef);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 从项目文件移除 ProjectReference 引用。
    /// </summary>
    /// <param name="projectPath">目标项目文件路径（.csproj）。</param>
    /// <param name="referencePath">要移除的项目引用路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>编辑操作结果。</returns>
    public async Task<ProjectEditResult> RemoveProjectReference(
        string projectPath,
        string referencePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        ArgumentException.ThrowIfNullOrEmpty(referencePath);

        return await ExecuteEditAsync(
            projectPath,
            "RemoveProjectReference",
            project =>
            {
                var relativeRef = GetRelativePath(projectPath, referencePath);
                s_logRemoveProjectRef(_logger, projectPath, relativeRef, null);

                var existing = project.GetItems("ProjectReference")
                    .FirstOrDefault(item => string.Equals(
                        item.EvaluatedInclude,
                        relativeRef,
                        StringComparison.OrdinalIgnoreCase));

                if (existing is null)
                {
                    throw new InvalidOperationException(
                        $"Project reference not found: {relativeRef}");
                }

                project.RemoveItem(existing);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 向项目文件添加 PackageReference 包引用。
    /// </summary>
    /// <param name="projectPath">目标项目文件路径（.csproj）。</param>
    /// <param name="packageId">NuGet 包 ID。</param>
    /// <param name="version">包版本。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>编辑操作结果。</returns>
    public async Task<ProjectEditResult> AddPackageReference(
        string projectPath,
        string packageId,
        string version,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        ArgumentException.ThrowIfNullOrEmpty(packageId);
        ArgumentException.ThrowIfNullOrEmpty(version);

        return await ExecuteEditAsync(
            projectPath,
            "AddPackageReference",
            project =>
            {
                s_logAddPackageRef(
                    _logger, projectPath, packageId, version, null);

                // 检查是否已存在相同包
                var existing = project.GetItems("PackageReference")
                    .FirstOrDefault(item => string.Equals(
                        item.EvaluatedInclude,
                        packageId,
                        StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    throw new InvalidOperationException(
                        $"Package reference already exists: {packageId}");
                }

                project.AddItem(
                    "PackageReference",
                    packageId,
                    new Dictionary<string, string>
                    {
                        { "Version", version }
                    });
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 更新项目中已有 PackageReference 的版本号。
    /// </summary>
    /// <param name="projectPath">目标项目文件路径（.csproj）。</param>
    /// <param name="packageId">要更新的 NuGet 包 ID。</param>
    /// <param name="newVersion">新的版本号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>编辑操作结果。</returns>
    public async Task<ProjectEditResult> UpdatePackageVersion(
        string projectPath,
        string packageId,
        string newVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        ArgumentException.ThrowIfNullOrEmpty(packageId);
        ArgumentException.ThrowIfNullOrEmpty(newVersion);

        return await ExecuteEditAsync(
            projectPath,
            "UpdatePackageVersion",
            project =>
            {
                s_logUpdatePackageVer(
                    _logger, projectPath, packageId, newVersion, null);

                var existing = project.GetItems("PackageReference")
                    .FirstOrDefault(item => string.Equals(
                        item.EvaluatedInclude,
                        packageId,
                        StringComparison.OrdinalIgnoreCase));

                if (existing is null)
                {
                    throw new InvalidOperationException(
                        $"Package reference not found: {packageId}");
                }

                existing.SetMetadataValue("Version", newVersion);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 修改项目文件中的 MSBuild 属性值。如果属性不存在则创建，已存在则更新。
    /// </summary>
    /// <param name="projectPath">目标项目文件路径（.csproj）。</param>
    /// <param name="propertyName">MSBuild 属性名。</param>
    /// <param name="value">属性值。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>编辑操作结果。</returns>
    public async Task<ProjectEditResult> ModifyProperty(
        string projectPath,
        string propertyName,
        string value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        ArgumentException.ThrowIfNullOrEmpty(value);

        return await ExecuteEditAsync(
            projectPath,
            "ModifyProperty",
            project =>
            {
                s_logModifyProperty(
                    _logger, projectPath, propertyName, value, null);
                project.SetProperty(propertyName, value);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 执行编辑操作的核心方法：验证路径、备份文件、执行编辑、保存结果。
    /// </summary>
    private async Task<ProjectEditResult> ExecuteEditAsync(
        string projectPath,
        string operationType,
        Action<Project> editAction,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // 验证路径安全性
            var validatedPath = PathValidator.ValidateProjectPath(projectPath);

            // 在磁盘 IO 线程上执行文件操作
            return await Task.Run(
                () => ExecuteEditCore(
                    validatedPath, operationType, editAction, sw),
                cancellationToken).ConfigureAwait(false);
        }
        catch (PathValidationException ex)
        {
            sw.Stop();
            s_logError(
                _logger, projectPath, operationType, ex);
            return new ProjectEditResult
            {
                Success = false,
                Message = "Path validation failed",
                OperationType = operationType,
                ProjectPath = projectPath,
                DurationMs = sw.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new ProjectEditResult
            {
                Success = false,
                Message = "Operation cancelled",
                OperationType = operationType,
                ProjectPath = projectPath,
                DurationMs = sw.ElapsedMilliseconds,
                Error = "Operation cancelled"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            s_logError(
                _logger, projectPath, operationType, ex);
            return new ProjectEditResult
            {
                Success = false,
                Message = $"Edit failed: {ex.Message}",
                OperationType = operationType,
                ProjectPath = projectPath,
                DurationMs = sw.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// 在文件系统线程上执行实际的文件读取、备份、编辑和保存。
    /// </summary>
    private ProjectEditResult ExecuteEditCore(
        string validatedPath,
        string operationType,
        Action<Project> editAction,
        Stopwatch sw)
    {
        string? backupPath = null;

        try
        {
            // 备份原始文件
            backupPath = BackupFile(validatedPath);

            // 使用 Project API 加载并编辑
            // 使用独立 ProjectCollection 避免全局状态污染
            var collection = new ProjectCollection();
            var project = new Project(
                validatedPath,
                null,
                null,
                collection);

            editAction(project);

            project.Save();

            // 释放资源
            collection.UnloadAllProjects();
            collection.Dispose();

            sw.Stop();
            s_logEditComplete(
                _logger, validatedPath, operationType,
                sw.Elapsed.TotalMilliseconds, null);

            return new ProjectEditResult
            {
                Success = true,
                Message = $"{operationType} completed successfully",
                OperationType = operationType,
                ProjectPath = validatedPath,
                BackupPath = backupPath,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            // 如果编辑失败，尝试从备份恢复
            if (backupPath is not null && File.Exists(backupPath))
            {
                try
                {
                    File.Copy(backupPath, validatedPath, overwrite: true);
                }
                catch
                {
                    // 恢复失败不影响错误报告
                }
            }

            s_logError(
                _logger, validatedPath, operationType, ex);
            return new ProjectEditResult
            {
                Success = false,
                Message = $"Edit failed: {ex.Message}",
                OperationType = operationType,
                ProjectPath = validatedPath,
                BackupPath = backupPath,
                DurationMs = sw.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// 创建项目文件的备份副本。
    /// </summary>
    /// <param name="projectPath">要备份的项目文件路径。</param>
    /// <returns>备份文件路径，如果备份失败则返回 null。</returns>
    private static string? BackupFile(string projectPath)
    {
        try
        {
            var backupPath = projectPath + ".bak";
            File.Copy(projectPath, backupPath, overwrite: true);
            return backupPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 将引用路径转换为相对于项目文件的相对路径。
    /// </summary>
    /// <param name="projectPath">项目文件路径。</param>
    /// <param name="referencePath">引用路径（绝对或相对）。</param>
    /// <returns>相对路径字符串。</returns>
    private static string GetRelativePath(string projectPath, string referencePath)
    {
        var projectDir = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException(
                $"Unable to get project directory: {projectPath}");

        // 如果已经是相对路径，直接返回
        if (!Path.IsPathRooted(referencePath))
        {
            return referencePath;
        }

        return Path.GetRelativePath(projectDir, referencePath);
    }
}
