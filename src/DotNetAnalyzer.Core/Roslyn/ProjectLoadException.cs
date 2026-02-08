namespace DotNetAnalyzer.Core.Roslyn;

/// <summary>
/// 项目或解决方案加载失败时抛出的异常
/// </summary>
public class ProjectLoadException : Exception
{
    public string? ProjectPath { get; }

    public ProjectLoadException(string message, string? projectPath = null)
        : base(message)
    {
        ProjectPath = projectPath;
    }

    public ProjectLoadException(string message, string projectPath, Exception innerException)
        : base(message, innerException)
    {
        ProjectPath = projectPath;
    }
}
