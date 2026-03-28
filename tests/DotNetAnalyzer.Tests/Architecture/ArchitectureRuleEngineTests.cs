using DotNetAnalyzer.Core.Architecture;
using DotNetAnalyzer.Core.Architecture.Models;
using DotNetAnalyzer.Core.Architecture.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetAnalyzer.Tests.Architecture;

/// <summary>
/// 架构规则检查引擎的综合测试
/// </summary>
public class ArchitectureRuleEngineTests
{
    private readonly NullLogger<ArchitectureRuleEngine> _logger =
        NullLogger<ArchitectureRuleEngine>.Instance;

    /// <summary>
    /// 创建带有指定源代码文档的测试项目
    /// </summary>
    private static Project CreateTestProject(
        Dictionary<string, string> files,
        string projectFilePath = "/project/TestProject.csproj")
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            filePath: projectFilePath);

        workspace.AddProject(projectInfo);

        foreach (var (fileName, source) in files)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            var documentInfo = DocumentInfo.Create(
                documentId,
                fileName,
                filePath: $"/project/{fileName}",
                sourceCodeKind: SourceCodeKind.Regular,
                loader: TextLoader.From(TextAndVersion.Create(
                    SourceText.From(source),
                    VersionStamp.Create())));

            workspace.AddDocument(documentInfo);
        }

        return workspace.CurrentSolution.GetProject(projectId)!;
    }

    // ========================================================
    // MatchesPattern 通过 EvaluateAsync 间接测试
    // ========================================================

    [Fact]
    public async Task MatchesPattern_WildcardMatch_DetectsViolation()
    {
        // Arrange: Core.Services 引用 Core.* (匹配 Core.Data)
        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                namespace Core.Services
                {
                    using Core.Data;
                    public class MyService { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new DependencyDirectionRule(
            "Core.*", "Core.Data", "error");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert: Core.Services 匹配 Core.* 模式，且引用了 Core.Data
        Assert.Single(violations);
    }

    [Fact]
    public async Task MatchesPattern_MidWildcardMatch_DetectsViolation()
    {
        // Arrange: MyApp.Core.Services 引用 MyApp.*.Services (匹配 MyApp.Utils.Services)
        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                namespace MyApp.Core.Services
                {
                    using MyApp.Utils.Services;
                    public class MyService { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new DependencyDirectionRule(
            "MyApp.*.Services", "MyApp.Utils.Services", "error");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert: MyApp.Core.Services 匹配 MyApp.*.Services 模式
        Assert.Single(violations);
    }

    [Fact]
    public async Task MatchesPattern_CaseInsensitive_DetectsViolation()
    {
        // Arrange: 小写命名空间也应匹配
        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                namespace core.services
                {
                    using Ui.Controllers;
                    public class MyService { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new DependencyDirectionRule(
            "Core.*", "Ui.*", "error");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert: 大小写不敏感匹配
        Assert.Single(violations);
    }

    [Fact]
    public async Task MatchesPattern_EmptyNamespace_SkipsFile()
    {
        // Arrange: 文件不在任何命名空间中
        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                using Ui.Controllers;
                public class MyService { }
                """
        };

        var project = CreateTestProject(files);
        var rule = new DependencyDirectionRule(
            "Core.*", "Ui.*", "error");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert: 空命名空间不匹配 Core.* 模式
        Assert.Empty(violations);
    }

    // ========================================================
    // DependencyDirectionRule 测试
    // ========================================================

    [Fact]
    public async Task DependencyDirectionRule_ViolatingUsing_ReturnsViolation()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                namespace Core.Services
                {
                    using Ui.Controllers;
                    public class MyService { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new DependencyDirectionRule(
            "Core.*", "Ui.*", "error");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert
        Assert.Single(violations);
        Assert.Equal("error", violations[0].Severity);
        Assert.Contains("Ui.Controllers", violations[0].Message);
    }

    [Fact]
    public async Task DependencyDirectionRule_NoViolation_ReturnsEmpty()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                namespace Core.Services
                {
                    using System;
                    using Core.Data;
                    public class MyService { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new DependencyDirectionRule(
            "Core.*", "Ui.*", "error");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public async Task DependencyDirectionRule_FileOutsideFromNamespace_Skipped()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["Other.cs"] = """
                namespace Other.Module
                {
                    using Ui.Controllers;
                    public class Other { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new DependencyDirectionRule(
            "Core.*", "Ui.*", "error");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public async Task DependencyDirectionRule_MultipleViolations_ReturnsAll()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                namespace Core.Services
                {
                    using Ui.Controllers;
                    using Ui.Views;
                    using Ui.Models;
                    public class MyService { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new DependencyDirectionRule(
            "Core.*", "Ui.*", "warning");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert
        Assert.Equal(3, violations.Count);
        Assert.All(violations, v => Assert.Equal("warning", v.Severity));
    }

    // ========================================================
    // LayerHierarchyRule 测试
    // ========================================================

    [Fact]
    public async Task LayerHierarchyRule_BackwardDependency_ReturnsViolation()
    {
        // Arrange: Api 引用 Core (反向依赖)。
        // forward-only 表示依赖只能从低索引流向高索引。
        // 层级按依赖方向排列：Core(0) -> Services(1) -> Api(2)。
        // Api(2) 引用 Core(0) 是 backward（2 > 0 不违规，但 Api 引用 Core
        // 意味着 Api 依赖 Core，即 Api 在高层应该依赖低层的 Core，
        // 这在 forward-only 中是合法的。
        // 实际上 forward-only 的语义是：只能引用比自己索引更大的层。
        // 所以 Core(0) 引用 Services(1) 是合法的（forward）。
        // 要测试 backward violation，需要高层引用低层，但 forward-only
        // 把高索引当作"上层"。
        // 修正：改为 Services 引用 Api 的测试场景 --
        // Services(1) 引用 Api(2) 在 forward-only 中是合法的（1<2）。
        // 要制造 violation，让 Api(2) 引用 Core(0)：
        // 2 > 0 所以 targetLayerIndex(0) <= sourceLayerIndex(2)，是 violation。
        var files = new Dictionary<string, string>
        {
            ["Controller.cs"] = """
                namespace MyProject.Api
                {
                    using MyProject.Core;
                    public class MyController { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new LayerHierarchyRule(
            new List<string> { "MyProject.Core", "MyProject.Services", "MyProject.Api" },
            "forward-only",
            "error");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert: Api(2) 引用 Core(0)，targetLayerIndex(0) <= sourceLayerIndex(2)
        Assert.Single(violations);
        Assert.Equal("error", violations[0].Severity);
        Assert.Contains("Core", violations[0].Message);
    }

    [Fact]
    public async Task LayerHierarchyRule_ForwardDependency_NoViolation()
    {
        // Arrange: Core 引用 Services (正向依赖，合法)。
        // 层级：Core(0), Services(1), Api(2)。
        // Core(0) 引用 Services(1)，targetLayerIndex(1) > sourceLayerIndex(0)，
        // 不是 violation。
        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                namespace MyProject.Core
                {
                    using MyProject.Services;
                    public class CoreClass { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new LayerHierarchyRule(
            new List<string> { "MyProject.Core", "MyProject.Services", "MyProject.Api" },
            "forward-only",
            "warning");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public async Task LayerHierarchyRule_SameLayerDependency_ReturnsViolation()
    {
        // Arrange: Services 引用 Services 下其他命名空间 (同层依赖)
        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                namespace MyProject.Services
                {
                    using MyProject.Services.Internal;
                    public class ServiceA { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new LayerHierarchyRule(
            new List<string> { "MyProject.Core", "MyProject.Services" },
            "forward-only",
            "warning");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert
        Assert.Single(violations);
    }

    [Fact]
    public async Task LayerHierarchyRule_UnsupportedDirection_ReturnsEmpty()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                namespace MyProject.Core
                {
                    using MyProject.Services;
                    public class CoreClass { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new LayerHierarchyRule(
            new List<string> { "MyProject.Core", "MyProject.Services" },
            "backward-only",
            "error");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert: 不支持的方向返回空结果
        Assert.Empty(violations);
    }

    // ========================================================
    // NamingConventionRule 测试
    // ========================================================

    [Fact]
    public async Task NamingConventionRule_ClassMatchingPattern_NoViolation()
    {
        // Arrange: 规则将 pattern 包裹为 ^pattern$，
        // 所以传入 ".*Controller$" 匹配以 Controller 结尾的类名
        var files = new Dictionary<string, string>
        {
            ["Controller.cs"] = """
                namespace MyApp.Controllers
                {
                    public class HomeController { }
                    public class UserController { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new NamingConventionRule(
            "class", ".*Controller$", "MyApp.Controllers", "warning");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public async Task NamingConventionRule_ClassNotMatchingPattern_ReturnsViolation()
    {
        // Arrange: 规则将 pattern 包裹为 ^pattern$，
        // 所以传入 ".*Controller$" 匹配以 Controller 结尾的类名
        var files = new Dictionary<string, string>
        {
            ["Controller.cs"] = """
                namespace MyApp.Controllers
                {
                    public class Helper { }
                    public class HomeController { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new NamingConventionRule(
            "class", ".*Controller$", "MyApp.Controllers", "warning");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert: 只有 Helper 不匹配 .*Controller$
        Assert.Single(violations);
        Assert.Contains("Helper", violations[0].Message);
    }

    [Fact]
    public async Task NamingConventionRule_InterfaceConvention_ChecksInterfaces()
    {
        // Arrange: 规则将 pattern 包裹为 ^pattern$，
        // 所以传入 "I[A-Z].*" 匹配以 I + 大写字母开头的接口名
        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                namespace MyApp.Abstractions
                {
                    public interface IService { }
                    public interface IRepository { }
                    public interface BadName { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new NamingConventionRule(
            "interface", "I[A-Z].*", "MyApp.Abstractions", "error");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert: 只有 BadName 不匹配 I[A-Z].*
        Assert.Single(violations);
        Assert.Contains("BadName", violations[0].Message);
    }

    [Fact]
    public async Task NamingConventionRule_MethodConvention_ChecksMethods()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                namespace MyApp.Services
                {
                    public class MyService
                    {
                        public void ProcessData() { }
                        public void GetData() { }
                        public void bad_name() { }
                    }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new NamingConventionRule(
            "method", "^[A-Z][a-zA-Z0-9]*$", "MyApp.Services", "warning");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert
        Assert.Single(violations);
        Assert.Contains("bad_name", violations[0].Message);
    }

    [Fact]
    public async Task NamingConventionRule_OutsideNamespace_Skipped()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["Other.cs"] = """
                namespace Other.Module
                {
                    public class Helper { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new NamingConventionRule(
            "class", "Controller$", "MyApp.Controllers", "warning");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public async Task NamingConventionRule_NoNamespaceFilter_ChecksAllFiles()
    {
        // Arrange: pattern 包裹为 ^pattern$，所以传入 ".*Controller$" 匹配
        // 以 Controller 结尾的类名
        var files = new Dictionary<string, string>
        {
            ["Any.cs"] = """
                namespace Any.Module
                {
                    public class Helper { }
                }
                """
        };

        var project = CreateTestProject(files);
        var rule = new NamingConventionRule(
            "class", ".*Controller$", null, "warning");

        // Act
        var violations = await rule.EvaluateAsync(project);

        // Assert: Helper 不匹配 .*Controller$ 模式
        Assert.Single(violations);
    }

    // ========================================================
    // ArchitectureRuleEngine.CheckAsync 规则创建测试
    // ========================================================

    [Fact]
    public async Task CheckAsync_DependencyDirectionConfig_CreatesAndEvaluatesRule()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"arc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var projectFilePath = Path.Combine(tempDir, "TestProject.csproj");
        File.WriteAllText(projectFilePath, string.Empty);

        var configJson = """
            {
              "rules": [
                {
                  "type": "dependency-direction",
                  "from": "Core.*",
                  "to": "Ui.*",
                  "severity": "error"
                }
              ]
            }
            """;
        var configPath = Path.Combine(tempDir, ArchitectureConfigReader.ConfigFileName);
        File.WriteAllText(configPath, configJson);

        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                namespace Core.Services
                {
                    using Ui.Controllers;
                    public class MyService { }
                }
                """
        };

        var project = CreateTestProject(files, projectFilePath);

        var mockLogger = new Mock<ILogger<ArchitectureConfigReader>>();
        var configReader = new ArchitectureConfigReader(mockLogger.Object);
        var engine = new ArchitectureRuleEngine(_logger, configReader);

        try
        {
            // Act
            var report = await engine.CheckAsync(project);

            // Assert: dependency-direction 规则被创建并执行
            Assert.NotNull(report);
            Assert.Equal(1, report.TotalRulesChecked);
            Assert.Equal(1, report.TotalViolations);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task CheckAsync_NamingConventionConfig_CreatesAndEvaluatesRule()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"arc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var projectFilePath = Path.Combine(tempDir, "TestProject.csproj");
        File.WriteAllText(projectFilePath, string.Empty);

        var configJson = """
            {
              "rules": [
                {
                  "type": "naming-convention",
                  "kind": "class",
                  "pattern": "Controller$",
                  "namespace": "MyApp.Controllers",
                  "severity": "warning"
                }
              ]
            }
            """;
        var configPath = Path.Combine(tempDir, ArchitectureConfigReader.ConfigFileName);
        File.WriteAllText(configPath, configJson);

        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                namespace MyApp.Controllers
                {
                    public class Helper { }
                }
                """
        };

        var project = CreateTestProject(files, projectFilePath);

        var mockLogger = new Mock<ILogger<ArchitectureConfigReader>>();
        var configReader = new ArchitectureConfigReader(mockLogger.Object);
        var engine = new ArchitectureRuleEngine(_logger, configReader);

        try
        {
            // Act
            var report = await engine.CheckAsync(project);

            // Assert: naming-convention 规则被创建并执行
            Assert.NotNull(report);
            Assert.Equal(1, report.TotalRulesChecked);
            Assert.Equal(1, report.TotalViolations);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task CheckAsync_LayerHierarchyConfig_CreatesAndEvaluatesRule()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"arc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var projectFilePath = Path.Combine(tempDir, "TestProject.csproj");
        File.WriteAllText(projectFilePath, string.Empty);

        var configJson = """
            {
              "rules": [
                {
                  "type": "layer-hierarchy",
                  "layers": ["Core", "Services", "Api"],
                  "allowedDirection": "forward-only",
                  "severity": "error"
                }
              ]
            }
            """;
        var configPath = Path.Combine(tempDir, ArchitectureConfigReader.ConfigFileName);
        File.WriteAllText(configPath, configJson);

        // Api(2) 引用 Core(0) 是 backward（targetLayerIndex <= sourceLayerIndex）
        var files = new Dictionary<string, string>
        {
            ["Controller.cs"] = """
                namespace Api
                {
                    using Core;
                    public class MyController { }
                }
                """
        };

        var project = CreateTestProject(files, projectFilePath);

        var mockLogger = new Mock<ILogger<ArchitectureConfigReader>>();
        var configReader = new ArchitectureConfigReader(mockLogger.Object);
        var engine = new ArchitectureRuleEngine(_logger, configReader);

        try
        {
            // Act
            var report = await engine.CheckAsync(project);

            // Assert: layer-hierarchy 规则被创建并执行
            Assert.NotNull(report);
            Assert.Equal(1, report.TotalRulesChecked);
            Assert.Equal(1, report.TotalViolations);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task CheckAsync_UnknownType_SkipsRule()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"arc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var projectFilePath = Path.Combine(tempDir, "TestProject.csproj");
        File.WriteAllText(projectFilePath, string.Empty);

        var configJson = """{ "rules": [{ "type": "unknown-type" }] }""";
        var configPath = Path.Combine(tempDir, ArchitectureConfigReader.ConfigFileName);
        File.WriteAllText(configPath, configJson);

        var project = CreateTestProject(
            new Dictionary<string, string>(),
            projectFilePath);

        var mockLogger = new Mock<ILogger<ArchitectureConfigReader>>();
        var configReader = new ArchitectureConfigReader(mockLogger.Object);
        var engine = new ArchitectureRuleEngine(_logger, configReader);

        try
        {
            // Act
            var report = await engine.CheckAsync(project);

            // Assert: unknown type 被跳过
            Assert.Equal(0, report.TotalRulesChecked);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task CheckAsync_MissingRequiredFields_SkipsRule()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"arc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var projectFilePath = Path.Combine(tempDir, "TestProject.csproj");
        File.WriteAllText(projectFilePath, string.Empty);

        var configJson = """
            {
              "rules": [
                { "type": "dependency-direction", "from": "Core.*", "severity": "error" },
                { "type": "layer-hierarchy", "allowedDirection": "forward-only" },
                { "type": "naming-convention", "kind": "class" }
              ]
            }
            """;
        var configPath = Path.Combine(tempDir, ArchitectureConfigReader.ConfigFileName);
        File.WriteAllText(configPath, configJson);

        var project = CreateTestProject(
            new Dictionary<string, string>(),
            projectFilePath);

        var mockLogger = new Mock<ILogger<ArchitectureConfigReader>>();
        var configReader = new ArchitectureConfigReader(mockLogger.Object);
        var engine = new ArchitectureRuleEngine(_logger, configReader);

        try
        {
            // Act
            var report = await engine.CheckAsync(project);

            // Assert: 缺少必填字段的规则被跳过
            Assert.Equal(0, report.TotalRulesChecked);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task CheckAsync_MultipleConfigs_CreatesMultipleRules()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"arc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var projectFilePath = Path.Combine(tempDir, "TestProject.csproj");
        File.WriteAllText(projectFilePath, string.Empty);

        var configJson = """
            {
              "rules": [
                {
                  "type": "dependency-direction",
                  "from": "Core.*",
                  "to": "Ui.*"
                },
                {
                  "type": "naming-convention",
                  "kind": "class",
                  "pattern": "Controller$"
                }
              ]
            }
            """;
        var configPath = Path.Combine(tempDir, ArchitectureConfigReader.ConfigFileName);
        File.WriteAllText(configPath, configJson);

        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                namespace Core.Services
                {
                    using Ui.Controllers;
                    public class Helper { }
                }
                """
        };

        var project = CreateTestProject(files, projectFilePath);

        var mockLogger = new Mock<ILogger<ArchitectureConfigReader>>();
        var configReader = new ArchitectureConfigReader(mockLogger.Object);
        var engine = new ArchitectureRuleEngine(_logger, configReader);

        try
        {
            // Act
            var report = await engine.CheckAsync(project);

            // Assert: 两条规则都被创建并执行
            Assert.Equal(2, report.TotalRulesChecked);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    // ========================================================
    // ArchitectureConfigReader 测试
    // ========================================================

    [Fact]
    public async Task ConfigReader_MissingFile_ReturnsEmptyList()
    {
        // Arrange
        var project = CreateTestProject(
            new Dictionary<string, string>(),
            "/project/NoRulesProject.csproj");

        var mockLogger = new Mock<ILogger<ArchitectureConfigReader>>();
        var reader = new ArchitectureConfigReader(mockLogger.Object);

        // Act
        var rules = await reader.ReadRulesAsync(project);

        // Assert
        Assert.Empty(rules);
    }

    [Fact]
    public async Task ConfigReader_InvalidJson_ThrowsInvalidDataException()
    {
        // Arrange: 创建临时目录和无效 JSON 文件
        var tempDir = Path.Combine(Path.GetTempPath(), $"arc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var projectFilePath = Path.Combine(tempDir, "TestProject.csproj");
        File.WriteAllText(projectFilePath, string.Empty);

        var configPath = Path.Combine(tempDir, ArchitectureConfigReader.ConfigFileName);
        File.WriteAllText(configPath, "{ invalid json }");

        var project = CreateTestProject(
            new Dictionary<string, string>(),
            projectFilePath);

        var mockLogger = new Mock<ILogger<ArchitectureConfigReader>>();
        var reader = new ArchitectureConfigReader(mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            () => reader.ReadRulesAsync(project));

        // Cleanup
        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public async Task ConfigReader_ValidJson_ReturnsRules()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"arc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var projectFilePath = Path.Combine(tempDir, "TestProject.csproj");
        File.WriteAllText(projectFilePath, string.Empty);

        var json = """
            {
              "rules": [
                {
                  "type": "dependency-direction",
                  "from": "Core.*",
                  "to": "Ui.*",
                  "severity": "error"
                },
                {
                  "type": "naming-convention",
                  "kind": "class",
                  "pattern": "Controller$",
                  "namespace": "MyApp.Controllers",
                  "severity": "warning"
                }
              ]
            }
            """;

        var configPath = Path.Combine(tempDir, ArchitectureConfigReader.ConfigFileName);
        File.WriteAllText(configPath, json);

        var project = CreateTestProject(
            new Dictionary<string, string>(),
            projectFilePath);

        var mockLogger = new Mock<ILogger<ArchitectureConfigReader>>();
        var reader = new ArchitectureConfigReader(mockLogger.Object);

        // Act
        var rules = await reader.ReadRulesAsync(project);

        // Assert
        Assert.Equal(2, rules.Count);
        Assert.Equal("dependency-direction", rules[0].Type);
        Assert.Equal("Core.*", rules[0].From);
        Assert.Equal("naming-convention", rules[1].Type);

        // Cleanup
        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public async Task ConfigReader_EmptyRulesArray_ReturnsEmptyList()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"arc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var projectFilePath = Path.Combine(tempDir, "TestProject.csproj");
        File.WriteAllText(projectFilePath, string.Empty);

        var json = """{ "rules": [] }""";
        var configPath = Path.Combine(tempDir, ArchitectureConfigReader.ConfigFileName);
        File.WriteAllText(configPath, json);

        var project = CreateTestProject(
            new Dictionary<string, string>(),
            projectFilePath);

        var mockLogger = new Mock<ILogger<ArchitectureConfigReader>>();
        var reader = new ArchitectureConfigReader(mockLogger.Object);

        // Act
        var rules = await reader.ReadRulesAsync(project);

        // Assert
        Assert.Empty(rules);

        // Cleanup
        try { Directory.Delete(tempDir, true); } catch { }
    }

    // ========================================================
    // ArchitectureRuleEngine.CheckAsync 端到端测试
    // ========================================================

    [Fact]
    public async Task CheckAsync_WithViolations_GeneratesReportWithViolations()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"arc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var projectFilePath = Path.Combine(tempDir, "TestProject.csproj");
        File.WriteAllText(projectFilePath, string.Empty);

        var configJson = """
            {
              "rules": [
                {
                  "type": "dependency-direction",
                  "from": "Core.*",
                  "to": "Ui.*",
                  "severity": "error"
                }
              ]
            }
            """;
        var configPath = Path.Combine(
            tempDir, ArchitectureConfigReader.ConfigFileName);
        File.WriteAllText(configPath, configJson);

        var files = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                namespace Core.Services
                {
                    using Ui.Controllers;
                    public class MyService { }
                }
                """
        };

        var project = CreateTestProject(files, projectFilePath);

        var mockLogger = new Mock<ILogger<ArchitectureConfigReader>>();
        var configReader = new ArchitectureConfigReader(mockLogger.Object);
        var engine = new ArchitectureRuleEngine(_logger, configReader);

        // Act
        var report = await engine.CheckAsync(project);

        // Assert
        Assert.NotNull(report);
        Assert.Equal(1, report.TotalRulesChecked);
        Assert.Equal(1, report.TotalViolations);
        Assert.Single(report.Violations);
        Assert.Equal("error", report.Violations[0].Severity);
        Assert.True(report.PassRate < 1.0);
        Assert.True(report.GeneratedAt <= DateTime.UtcNow);

        // Cleanup
        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public async Task CheckAsync_NoViolations_ReportShowsFullPassRate()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"arc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var projectFilePath = Path.Combine(tempDir, "TestProject.csproj");
        File.WriteAllText(projectFilePath, string.Empty);

        var configJson = """
            {
              "rules": [
                {
                  "type": "naming-convention",
                  "kind": "class",
                  "pattern": ".*Controller$",
                  "namespace": "MyApp.Controllers",
                  "severity": "warning"
                }
              ]
            }
            """;
        var configPath = Path.Combine(
            tempDir, ArchitectureConfigReader.ConfigFileName);
        File.WriteAllText(configPath, configJson);

        var files = new Dictionary<string, string>
        {
            ["HomeController.cs"] = """
                namespace MyApp.Controllers
                {
                    public class HomeController { }
                }
                """
        };

        var project = CreateTestProject(files, projectFilePath);

        var mockLogger = new Mock<ILogger<ArchitectureConfigReader>>();
        var configReader = new ArchitectureConfigReader(mockLogger.Object);
        var engine = new ArchitectureRuleEngine(_logger, configReader);

        // Act
        var report = await engine.CheckAsync(project);

        // Assert
        Assert.NotNull(report);
        Assert.Equal(1, report.TotalRulesChecked);
        Assert.Equal(0, report.TotalViolations);
        Assert.Empty(report.Violations);
        Assert.Equal(1.0, report.PassRate);

        // Cleanup
        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public async Task CheckAsync_NoConfigFile_ReportShowsNoRulesChecked()
    {
        // Arrange
        var project = CreateTestProject(
            new Dictionary<string, string>
            {
                ["A.cs"] = "namespace A { public class B { } }"
            });

        var mockLogger = new Mock<ILogger<ArchitectureConfigReader>>();
        var configReader = new ArchitectureConfigReader(mockLogger.Object);
        var engine = new ArchitectureRuleEngine(_logger, configReader);

        // Act
        var report = await engine.CheckAsync(project);

        // Assert
        Assert.NotNull(report);
        Assert.Equal(0, report.TotalRulesChecked);
        Assert.Equal(0, report.TotalViolations);
        Assert.Equal(1.0, report.PassRate);
    }
}
