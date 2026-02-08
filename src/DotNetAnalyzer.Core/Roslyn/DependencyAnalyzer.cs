using Microsoft.CodeAnalysis;

namespace DotNetAnalyzer.Core.Roslyn;

/// <summary>
/// 项目依赖关系分析器 - 分析项目引用和包依赖
/// </summary>
public static class DependencyAnalyzer
{
    /// <summary>
    /// 分析单个项目的依赖关系
    /// </summary>
    public static ProjectDependencyInfo AnalyzeDependencies(Project project)
    {
        if (project == null)
            throw new ArgumentNullException(nameof(project));

        // 从编译选项中获取目标框架
        var targetFramework = "Unknown";
        if (project.CompilationOptions != null)
        {
            var tfm = project.CompilationOptions.OutputKind.ToString();
            targetFramework = tfm;
        }

        return new ProjectDependencyInfo
        {
            ProjectName = project.Name,
            ProjectFilePath = project.FilePath,
            TargetFramework = targetFramework,
            ProjectReferences = GetProjectReferences(project),
            PackageReferences = GetPackageReferences(project),
            TransitiveDependencies = GetTransitiveDependencies(project),
            HasCircularReference = HasCircularDependency(project)
        };
    }

    /// <summary>
    /// 获取项目引用（ProjectReference）
    /// </summary>
    private static ProjectReferenceInfo[] GetProjectReferences(Project project)
    {
        return project.ProjectReferences
            .Select(pr =>
            {
                var referencedProject = project.Solution.GetProject(pr.ProjectId);
                return new ProjectReferenceInfo
                {
                    ProjectName = referencedProject?.Name ?? "Unknown",
                    ProjectFilePath = referencedProject?.FilePath,
                    Alias = pr.Aliases.FirstOrDefault() ?? string.Empty
                };
            })
            .ToArray();
    }

    /// <summary>
    /// 获取包引用（PackageReference）
    /// </summary>
    private static PackageReferenceInfo[] GetPackageReferences(Project project)
    {
        // 从 MSBuild 属性中提取包引用
        var packages = new List<PackageReferenceInfo>();

        // 获取所有 NuGet 包引用
        foreach (var reference in project.MetadataReferences.OfType<PortableExecutableReference>())
        {
            var filePath = reference.FilePath;
            if (filePath != null && filePath.Contains(".nuget\\packages\\"))
            {
                var parts = filePath.Split(".nuget\\packages\\");
                if (parts.Length > 1)
                {
                    var packageParts = parts[1].Split('\\');
                    if (packageParts.Length >= 2)
                    {
                        var packageName = packageParts[0];
                        var version = packageParts[1];

                        // 避免重复
                        if (!packages.Any(p => p.Name == packageName))
                        {
                            packages.Add(new PackageReferenceInfo
                            {
                                Name = packageName,
                                Version = version,
                                IsTransitive = false
                            });
                        }
                    }
                }
            }
        }

        return packages.ToArray();
    }

    /// <summary>
    /// 获取传递依赖（间接依赖）
    /// </summary>
    private static DependencyInfo[] GetTransitiveDependencies(Project project)
    {
        var transitive = new List<string>();
        var visited = new HashSet<string>();

        CollectTransitiveDependencies(project, visited, transitive);

        return transitive
            .Distinct()
            .Select(name => new DependencyInfo { Name = name })
            .ToArray();
    }

    /// <summary>
    /// 递归收集传递依赖
    /// </summary>
    private static void CollectTransitiveDependencies(Project project, HashSet<string> visited, List<string> transitive)
    {
        foreach (var projectRef in project.ProjectReferences)
        {
            var referencedProject = project.Solution.GetProject(projectRef.ProjectId);
            if (referencedProject == null) continue;

            if (!visited.Contains(referencedProject.Name))
            {
                visited.Add(referencedProject.Name);
                transitive.Add(referencedProject.Name);
                CollectTransitiveDependencies(referencedProject, visited, transitive);
            }
        }
    }

    /// <summary>
    /// 检查是否存在循环依赖
    /// </summary>
    private static bool HasCircularDependency(Project project)
    {
        var visited = new HashSet<ProjectId>();
        var recursionStack = new HashSet<ProjectId>();

        return HasCircularDependencyRecursive(project, visited, recursionStack);
    }

    /// <summary>
    /// 递归检查循环依赖
    /// </summary>
    private static bool HasCircularDependencyRecursive(Project project, HashSet<ProjectId> visited, HashSet<ProjectId> recursionStack)
    {
        if (recursionStack.Contains(project.Id))
            return true;

        if (visited.Contains(project.Id))
            return false;

        visited.Add(project.Id);
        recursionStack.Add(project.Id);

        foreach (var projectRef in project.ProjectReferences)
        {
            var referencedProject = project.Solution.GetProject(projectRef.ProjectId);
            if (referencedProject != null && HasCircularDependencyRecursive(referencedProject, visited, recursionStack))
                return true;
        }

        recursionStack.Remove(project.Id);
        return false;
    }
}

#region Data Models

/// <summary>
/// 项目依赖信息
/// </summary>
public class ProjectDependencyInfo
{
    public string ProjectName { get; set; } = string.Empty;
    public string? ProjectFilePath { get; set; }
    public string TargetFramework { get; set; } = string.Empty;
    public ProjectReferenceInfo[] ProjectReferences { get; set; } = Array.Empty<ProjectReferenceInfo>();
    public PackageReferenceInfo[] PackageReferences { get; set; } = Array.Empty<PackageReferenceInfo>();
    public DependencyInfo[] TransitiveDependencies { get; set; } = Array.Empty<DependencyInfo>();
    public bool HasCircularReference { get; set; }
}

/// <summary>
/// 项目引用信息
/// </summary>
public class ProjectReferenceInfo
{
    public string ProjectName { get; set; } = string.Empty;
    public string? ProjectFilePath { get; set; }
    public string Alias { get; set; } = string.Empty;
}

/// <summary>
/// 包引用信息
/// </summary>
public class PackageReferenceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsTransitive { get; set; }
}

/// <summary>
/// 依赖项信息
/// </summary>
public class DependencyInfo
{
    public string Name { get; set; } = string.Empty;
}

#endregion
