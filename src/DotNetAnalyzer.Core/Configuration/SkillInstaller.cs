namespace DotNetAnalyzer.Core.Configuration;

/// <summary>
/// 技能文件安装器
/// </summary>
public class SkillInstaller
{
    private readonly string? _pluginPath;

    /// <summary>
    /// 创建技能安装器
    /// </summary>
    /// <param name="pluginPath">plugin 目录路径，null 则自动检测</param>
    public SkillInstaller(string? pluginPath = null)
    {
        _pluginPath = pluginPath;
    }

    /// <summary>
    /// 安装技能文件到项目
    /// </summary>
    /// <param name="projectPath">项目路径</param>
    /// <param name="output">输出写入器</param>
    /// <returns>安装结果</returns>
    public async Task<SkillInstallResult> InstallAsync(string projectPath, TextWriter? output = null)
    {
        var result = new SkillInstallResult();

        try
        {
            // 1. 查找模板/插件目录
            var basePath = await FindPluginDirectoryAsync();
            if (string.IsNullOrEmpty(basePath))
            {
                result.Success = false;
                result.ErrorMessage = "未找到 DotNetAnalyzer 模板目录";
                return result;
            }

            // 2. 查找技能文件
            // 尝试两种可能的技能目录位置
            var possibleSkillsDirs = new[]
            {
                Path.Combine(basePath, "claude", "skills"),      // templates/claude/skills
                Path.Combine(basePath, ".claude", "skills"),     // plugin/.claude/skills
                Path.Combine(basePath, "plugin", ".claude", "skills") // templates/plugin/.claude/skills
            };

            var skillsDir = possibleSkillsDirs.FirstOrDefault(Directory.Exists);
            if (string.IsNullOrEmpty(skillsDir))
            {
                result.Success = false;
                result.ErrorMessage = $"未找到技能文件夹（尝试了 {string.Join(", ", possibleSkillsDirs.Select(Path.GetFileName))}）";
                return result;
            }

            var skillDirectories = Directory.GetDirectories(skillsDir);
            if (skillDirectories.Length == 0)
            {
                result.Success = false;
                result.ErrorMessage = "plugin 目录中没有找到任何技能";
                return result;
            }

            // 3. 创建目标目录
            var targetSkillsDir = Path.Combine(projectPath, ".claude", "skills");
            Directory.CreateDirectory(targetSkillsDir);

            // 4. 复制技能文件
            foreach (var sourceSkillDir in skillDirectories)
            {
                var skillName = Path.GetFileName(sourceSkillDir);
                var targetSkillDir = Path.Combine(targetSkillsDir, skillName);

                // 如果目标已存在，先删除
                if (Directory.Exists(targetSkillDir))
                {
                    Directory.Delete(targetSkillDir, recursive: true);
                }

                // 复制整个技能目录
                CopyDirectoryRecursive(sourceSkillDir, targetSkillDir);
                result.InstalledSkills.Add(skillName);

                output?.WriteLine($"  ✓ 已安装技能: {skillName}");
            }

            result.Success = true;
            result.SkillsDirectory = targetSkillsDir;

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// 查找 plugin 目录
    /// </summary>
    private async Task<string?> FindPluginDirectoryAsync()
    {
        // 如果显式指定了路径，直接使用
        if (!string.IsNullOrEmpty(_pluginPath))
        {
            return Directory.Exists(_pluginPath) ? _pluginPath : null;
        }

        var currentDir = Directory.GetCurrentDirectory();

        // 1. 优先查找 templates/claude/skills/（源代码位置，不会被 .gitignore 忽略）
        var templatesSkillsDir = Path.Combine(currentDir, "templates", "claude", "skills");
        if (Directory.Exists(templatesSkillsDir))
        {
            return Path.Combine(currentDir, "templates");
        }

        // 2. 尝试其他可能的路径
        var possiblePaths = new List<string>
        {
            // 开发环境：dist/plugin（构建输出，可能被忽略）
            Path.Combine(currentDir, "dist", "plugin"),

            // 安装后的全局位置
            Path.Combine(currentDir, ".dotnet", "tools", "dotnet-analyzer", "plugin"),

            // 用户主目录下的安装位置
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".dotnet", "tools", "dotnet-analyzer", "plugin")
        };

        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                // 检查是否包含技能目录
                var skillsDir = Path.Combine(path, ".claude", "skills");
                if (Directory.Exists(skillsDir))
                {
                    return path;
                }

                // 也检查 templates 子目录
                var templatesDir = Path.Combine(path, "templates", "claude", "skills");
                if (Directory.Exists(templatesDir))
                {
                    return path;
                }
            }
        }

        // 3. 尝试从 dotnet-analyzer 的位置查找
        try
        {
            var detector = new EnvironmentDetector();
            var env = await detector.DetectAsync();
            var analyzerPath = env.DotnetAnalyzerPath;

            if (!string.IsNullOrEmpty(analyzerPath))
            {
                // 获取工具所在目录
                var toolDir = Path.GetDirectoryName(analyzerPath);
                if (!string.IsNullOrEmpty(toolDir))
                {
                    // 检查工具目录旁边的 templates 目录
                    var templatesDir = Path.Combine(toolDir, "..", "..", "templates");
                    if (Directory.Exists(templatesDir))
                    {
                        var skillsPath = Path.Combine(templatesDir, "claude", "skills");
                        if (Directory.Exists(skillsPath))
                        {
                            return Path.GetFullPath(templatesDir);
                        }
                    }

                    // 检查 plugin 目录
                    var pluginDir = Path.Combine(toolDir, "plugin");
                    if (Directory.Exists(pluginDir))
                    {
                        var skillsDir = Path.Combine(pluginDir, ".claude", "skills");
                        if (Directory.Exists(skillsDir))
                        {
                            return pluginDir;
                        }
                    }
                }
            }
        }
        catch
        {
            // 忽略错误，继续尝试其他方法
        }

        return null;
    }

    /// <summary>
    /// 递归复制目录
    /// </summary>
    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        // 复制文件
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(targetDir, fileName);
            File.Copy(file, destFile, overwrite: true);
        }

        // 递归复制子目录
        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(directory);
            var destDir = Path.Combine(targetDir, dirName);
            CopyDirectoryRecursive(directory, destDir);
        }
    }

    /// <summary>
    /// 获取可用的技能列表
    /// </summary>
    /// <returns>技能名称列表</returns>
    public async Task<string[]> GetAvailableSkillsAsync()
    {
        var basePath = await FindPluginDirectoryAsync();
        if (string.IsNullOrEmpty(basePath))
        {
            return Array.Empty<string>();
        }

        // 尝试多种可能的技能目录位置
        var possibleSkillsDirs = new[]
        {
            Path.Combine(basePath, "claude", "skills"),      // templates/claude/skills
            Path.Combine(basePath, ".claude", "skills"),     // plugin/.claude/skills
            Path.Combine(basePath, "plugin", ".claude", "skills") // templates/plugin/.claude/skills
        };

        foreach (var skillsDir in possibleSkillsDirs)
        {
            if (Directory.Exists(skillsDir))
            {
                return Directory.GetDirectories(skillsDir)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Cast<string>()
                    .ToArray()!;
            }
        }

        return Array.Empty<string>();
    }
}

/// <summary>
/// 技能安装结果
/// </summary>
public record SkillInstallResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 已安装的技能列表
    /// </summary>
    public List<string> InstalledSkills { get; set; } = new();

    /// <summary>
    /// 技能目录路径
    /// </summary>
    public string? SkillsDirectory { get; set; }
}
