using DotNetAnalyzer.Core.Configuration;

namespace DotNetAnalyzer.Cli.Commands;

/// <summary>
/// Init 命令处理器
/// </summary>
public static class InitCommandHandler
{
    /// <summary>
    /// 执行初始化
    /// </summary>
    public static async Task<int> ExecuteAsync(InitOptions options, TextWriter output, TextReader input)
    {
        try
        {
            // 1. 检测环境（在 dry-run 模式下使用默认环境信息）
            EnvironmentInfo env;
            if (options.DryRun)
            {
                // Dry-run 模式：使用默认环境信息，避免检测失败
                env = new EnvironmentInfo
                {
                    DotnetAnalyzerPath = "dotnet-analyzer",
                    DotnetSdkVersion = "8.0.0",
                    OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "Windows" :
                                     System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) ? "macOS" : "Linux",
                    ShellType = "sh",
                    ProjectFiles = Array.Empty<string>(),
                    ExistingConfig = new ExistingConfigInfo()
                };
            }
            else
            {
                env = await EnvironmentDetector.DetectAsync();
            }

            if (options.Verbose)
            {
                output.WriteLine("环境检测完成:");
                output.WriteLine($"  .NET SDK: {env.DotnetSdkVersion}");
                output.WriteLine($"  OS: {env.OperatingSystem}");
                output.WriteLine($"  dotnet-analyzer: {env.DotnetAnalyzerPath}");
            }

            // 2. 检查现有配置
            if (!options.Force)
            {
                if (env.ExistingConfig.HasClaudeSettings || env.ExistingConfig.HasMcpJson)
                {
                    output.WriteLine("⚠️  检测到现有配置:");
                    if (env.ExistingConfig.HasClaudeSettings)
                        output.WriteLine("  - .claude/settings.json");
                    if (env.ExistingConfig.HasMcpJson)
                        output.WriteLine("  - .mcp.json");
                    output.WriteLine();
                    output.WriteLine("使用 --force 选项覆盖现有配置");
                    return 1;
                }
            }

            // 3. 交互式向导（如果不是 --yes 模式）
            var finalOptions = options;
            if (!options.Yes)
            {
                finalOptions = await InitWizard.RunAsync(env, options, output, input);
            }

            // 4. 生成配置
            var configResult = await ConfigGenerator.GenerateConfigsAsync(finalOptions, env);

            if (options.DryRun)
            {
                output.WriteLine("📋 预览 - 将要生成的配置:");
                output.WriteLine($"  范围: {finalOptions.Scope}");
                output.WriteLine("  文件:");
                if (finalOptions.Scope == "project")
                {
                    output.WriteLine("    - .mcp.json");
                    output.WriteLine("    - .claude/settings.json");
                }
                else
                {
                    output.WriteLine("    - ~/.claude/settings.json");
                }
                return 0;
            }

            // 写入配置文件
            var configDir = !string.IsNullOrEmpty(finalOptions.Output)
                ? finalOptions.Output
                : Directory.GetCurrentDirectory();
            await WriteConfigFilesAsync(configDir, finalOptions.Scope, configResult);

            // 4.5 安装技能文件（仅项目级配置）
            if (finalOptions.Scope == "project")
            {
                var skillInstaller = new SkillInstaller();
                output.WriteLine();
                output.WriteLine("安装技能文件...");

                var installResult = await skillInstaller.InstallAsync(configDir, output);

                if (!installResult.Success)
                {
                    output.WriteLine($"⚠️  技能安装失败: {installResult.ErrorMessage}");
                    output.WriteLine("  技能文件未安装，但配置文件已生成。");
                    output.WriteLine("  您可以手动复制技能文件或重新运行 init 命令。");
                }
                else if (installResult.InstalledSkills.Count > 0)
                {
                    output.WriteLine($"✅ 已安装 {installResult.InstalledSkills.Count} 个技能:");
                    foreach (var skill in installResult.InstalledSkills)
                    {
                        output.WriteLine($"  - {skill}");
                    }
                }
                else
                {
                    output.WriteLine("ℹ️  未找到预定义技能文件");
                }
            }

            // 5. 验证配置
            int exitCode = 0;
            if (options.Verify)
            {
                var validator = new ConfigValidator();
                var validationResult = await ConfigValidator.ValidateAsync(configResult);

                if (!validationResult.IsValid)
                {
                    output.WriteLine("⚠️  配置验证发现问题:");
                    foreach (var check in validationResult.Checks.Where(c => !c.Passed))
                    {
                        output.WriteLine($"  ❌ {check.Name}: {check.Error}");
                    }
                    exitCode = 1;
                }
                else
                {
                    output.WriteLine("✅ 配置验证通过");
                }
            }

            // 6. 显示完成信息
            output.WriteLine();
            output.WriteLine("✅ DotNetAnalyzer 配置完成!");
            output.WriteLine();
            output.WriteLine("下一步:");
            if (finalOptions.Scope == "project")
            {
                output.WriteLine("  1. 重启 Claude Code");
                output.WriteLine("  2. 打开此项目");
                output.WriteLine("  3. 开始使用: '请分析这个项目的代码质量'");
            }
            else
            {
                output.WriteLine("  1. 重启 Claude Code");
                output.WriteLine("  2. 打开任何 .NET 项目");
                output.WriteLine("  3. 开始使用: '请分析这个项目的代码质量'");
            }
            output.WriteLine();

            return exitCode;
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ 错误: {ex.Message}");
            if (options.Verbose)
            {
                output.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    /// <summary>
    /// 写入配置文件
    /// </summary>
    private static async Task WriteConfigFilesAsync(string configDir, string scope, ConfigGenerationResult result)
    {
        if (scope == "project")
        {
            // 写入 .mcp.json
            var mcpPath = Path.Combine(configDir, ".mcp.json");
            await File.WriteAllTextAsync(mcpPath, result.McpConfigJson);

            // 写入 .claude/settings.json（项目级，可以直接覆盖或创建）
            var claudeDir = Path.Combine(configDir, ".claude");
            Directory.CreateDirectory(claudeDir);
            var settingsPath = Path.Combine(claudeDir, "settings.json");

            // 如果已有配置，合并
            var mergedSettings = await MergeSettingsAsync(settingsPath, result);
            await File.WriteAllTextAsync(settingsPath, mergedSettings);
        }
        else
        {
            // 用户级配置 - 写入 ~/.claude/settings.json
            // 必须合并现有配置，不能覆盖！
            var userClaudeDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude");
            Directory.CreateDirectory(userClaudeDir);
            var settingsPath = Path.Combine(userClaudeDir, "settings.json");

            var mergedSettings = await MergeSettingsAsync(settingsPath, result);
            await File.WriteAllTextAsync(settingsPath, mergedSettings);
        }
    }

    /// <summary>
    /// 合并现有配置与新配置
    /// </summary>
    private static async Task<string> MergeSettingsAsync(string settingsPath, ConfigGenerationResult result)
    {
        // 如果文件不存在，直接返回新配置
        if (!File.Exists(settingsPath))
        {
            return result.SettingsJson;
        }

        try
        {
            // 读取现有配置
            var existingJson = await File.ReadAllTextAsync(settingsPath);
            var existingNode = System.Text.Json.Nodes.JsonNode.Parse(existingJson) as System.Text.Json.Nodes.JsonObject;
            var newNode = System.Text.Json.Nodes.JsonNode.Parse(result.SettingsJson) as System.Text.Json.Nodes.JsonObject;

            if (existingNode == null || newNode == null)
            {
                return result.SettingsJson;
            }

            // 递归深度合并
            MergeJsonObjects(existingNode, newNode);

            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            return existingNode.ToJsonString(options);
        }
        catch
        {
            // 解析失败，返回新配置
            return result.SettingsJson;
        }
    }

    /// <summary>
    /// 递归深度合并两个 JSON 对象
    /// </summary>
    private static void MergeJsonObjects(System.Text.Json.Nodes.JsonObject existing, System.Text.Json.Nodes.JsonObject @new)
    {
        foreach (var property in @new)
        {
            var key = property.Key;
            var newValue = property.Value;

            // 如果现有对象中没有这个 key，直接添加
            if (!existing.ContainsKey(key))
            {
                existing[key] = newValue?.DeepClone();
                continue;
            }

            var existingValue = existing[key];

            // 两个都是对象：递归合并
            if (newValue is System.Text.Json.Nodes.JsonObject newobj &&
                existingValue is System.Text.Json.Nodes.JsonObject existingObj)
            {
                MergeJsonObjects(existingObj, newobj);
            }
            // 两个都是数组：合并去重
            else if (newValue is System.Text.Json.Nodes.JsonArray newArray &&
                     existingValue is System.Text.Json.Nodes.JsonArray existingArray)
            {
                var mergedArray = MergeArrays(existingArray, newArray);
                existing[key] = mergedArray;
            }
            // 其他类型：新值覆盖旧值（但这种情况通常不会发生在 permissions 上）
            else
            {
                existing[key] = newValue?.DeepClone();
            }
        }
    }

    /// <summary>
    /// 合并两个数组（去重）
    /// </summary>
    private static System.Text.Json.Nodes.JsonArray MergeArrays(
        System.Text.Json.Nodes.JsonArray existing,
        System.Text.Json.Nodes.JsonArray @new)
    {
        var mergedArray = new System.Text.Json.Nodes.JsonArray();
        var seen = new HashSet<string>();

        // 先添加现有元素
        foreach (var item in existing)
        {
            if (item?.ToString() is { } str && seen.Add(str))
            {
                mergedArray.Add(str);
            }
        }

        // 再添加新元素
        foreach (var item in @new)
        {
            if (item?.ToString() is { } str && seen.Add(str))
            {
                mergedArray.Add(str);
            }
        }

        return mergedArray;
    }
}
