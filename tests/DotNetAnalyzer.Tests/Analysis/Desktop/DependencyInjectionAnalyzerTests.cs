using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging.Abstractions;
using DotNetAnalyzer.Core.Analysis.Desktop;
using DotNetAnalyzer.Core.Analysis.Desktop.Models;
using FluentAssertions;
using Xunit;

namespace DotNetAnalyzer.Tests.Analysis.Desktop;

/// <summary>
/// DependencyInjectionAnalyzer 单元测试。
/// </summary>
/// <remarks>
/// 覆盖 DI 注册收集、缺少注册检测、空项目和多种构造函数场景。
/// </remarks>
public class DependencyInjectionAnalyzerTests
{
    private readonly DependencyInjectionAnalyzer _analyzer;

    public DependencyInjectionAnalyzerTests()
    {
        _analyzer = new DependencyInjectionAnalyzer(
            NullLogger<DependencyInjectionAnalyzer>.Instance);
    }

    #region 辅助方法

    /// <summary>
    /// 创建带有单个文档的测试项目。
    /// </summary>
    private static async Task<Project> CreateProjectAsync(
        string sourceCode,
        string fileName = "Test.cs")
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var versionStamp = VersionStamp.Create();

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(System.Runtime.CompilerServices.TaskAwaiter).Assembly.Location),
        };

        var projectInfo = ProjectInfo.Create(
            projectId,
            versionStamp,
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            metadataReferences: references);

        workspace.AddProject(projectInfo);

        var documentInfo = DocumentInfo.Create(
            documentId,
            fileName,
            filePath: $"/{fileName}",
            sourceCodeKind: SourceCodeKind.Regular,
            loader: TextLoader.From(TextAndVersion.Create(
                SourceText.From(sourceCode),
                versionStamp)));

        workspace.AddDocument(documentInfo);

        var project = workspace.CurrentSolution.GetProject(projectId)!;
        return project;
    }

    #endregion

    #region DI 注册收集

    [Fact]
    public async Task AnalyzeAsync_WithRegistrations_ReturnsRegistrations()
    {
        // Arrange
        // 使用 AddSingleton 和 AddScoped 分别注册两个服务
        var source = """
            public class Startup
            {
                public void ConfigureService(IServiceCollection services)
                {
                    services.AddSingleton<IMyService, MyService>();
                    services.AddScoped<IRepository, SqlRepository>();
                }
            }

            public interface IMyService { }
            public class MyService : IMyService { }
            public interface IRepository { }
            public class SqlRepository : IRepository { }
            public class ServiceCollection { }
            public static class ServiceCollectionExtensions
            {
                public static ServiceCollection AddSingleton<TService, TImplementation>(this ServiceCollection services) => services;
                public static ServiceCollection AddScoped<TService, TImplementation>(this ServiceCollection services) => services;
                public static ServiceCollection AddTransient<TService, TImplementation>(this ServiceCollection services) => services;
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert
        result.TotalRegistrations.Should().Be(2);
        result.Registrations.Should().Contain(r =>
            r.ServiceType == "IMyService" &&
            r.Lifetime == DiLifetime.Singleton);
        result.Registrations.Should().Contain(r =>
            r.ServiceType == "IRepository" &&
            r.Lifetime == DiLifetime.Scoped);
    }

    #endregion

    #region 缺少注册检测

    [Fact]
    public async Task AnalyzeAsync_MissingRegistration_DetectsMissing()
    {
        // Arrange
        // MyService 已注册，但其构造函数需要 IEmailService（未注册）
        var source = """
            public class Startup
            {
                public void ConfigureService(IServiceCollection services)
                {
                    services.AddSingleton<IMyService, MyService>();
                }
            }

            public interface IMyService { }
            public interface IEmailService { }
            public class MyService : IMyService
            {
                public MyService(IEmailService emailService) { }
            }
            public class ServiceCollection { }
            public static class ServiceCollectionExtensions
            {
                public static ServiceCollection AddSingleton<TService, TImplementation>(this ServiceCollection services) => services;
                public static ServiceCollection AddScoped<TService, TImplementation>(this ServiceCollection services) => services;
                public static ServiceCollection AddTransient<TService, TImplementation>(this ServiceCollection services) => services;
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert
        result.TotalMissing.Should().Be(1);
        result.MissingRegistrations.Should().Contain(m =>
            m.ServiceType == "IEmailService");
    }

    [Fact]
    public async Task AnalyzeAsync_AllRegistered_NoMissing()
    {
        // Arrange
        // MyService 和 IRepository 都已注册，MyService 构造函数依赖 IRepository
        var source = """
            public class Startup
            {
                public void ConfigureService(IServiceCollection services)
                {
                    services.AddSingleton<IMyService, MyService>();
                    services.AddScoped<IRepository, SqlRepository>();
                }
            }

            public interface IMyService { }
            public interface IRepository { }
            public class MyService : IMyService
            {
                public MyService(IRepository repository) { }
            }
            public class SqlRepository : IRepository { }
            public class ServiceCollection { }
            public static class ServiceCollectionExtensions
            {
                public static ServiceCollection AddSingleton<TService, TImplementation>(this ServiceCollection services) => services;
                public static ServiceCollection AddScoped<TService, TImplementation>(this ServiceCollection services) => services;
                public static ServiceCollection AddTransient<TService, TImplementation>(this ServiceCollection services) => services;
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert — IRepository 已注册，MyService 不应报告缺少依赖
        result.TotalMissing.Should().Be(0);
        result.MissingRegistrations.Should().BeEmpty();
    }

    #endregion

    #region 边界情况

    [Fact]
    public async Task AnalyzeAsync_EmptyProject_ReturnsEmptyResults()
    {
        // Arrange
        // 空类，不包含任何 DI 注册或构造函数
        var source = """
            public class Empty { }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert
        result.TotalRegistrations.Should().Be(0);
        result.TotalMissing.Should().Be(0);
        result.Registrations.Should().BeEmpty();
        result.MissingRegistrations.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_ParameterlessConstructor_NoMissingForIt()
    {
        // Arrange
        // MyService 同时有无参数构造函数和带参数构造函数。
        // IEmailService 未注册，但 MyService 可以通过无参数构造函数实例化。
        var source = """
            public class Startup
            {
                public void ConfigureService(IServiceCollection services)
                {
                    services.AddSingleton<IMyService, MyService>();
                }
            }

            public interface IMyService { }
            public interface IEmailService { }
            public class MyService : IMyService
            {
                public MyService() { }

                public MyService(IEmailService emailService) { }
            }
            public class ServiceCollection { }
            public static class ServiceCollectionExtensions
            {
                public static ServiceCollection AddSingleton<TService, TImplementation>(this ServiceCollection services) => services;
                public static ServiceCollection AddScoped<TService, TImplementation>(this ServiceCollection services) => services;
                public static ServiceCollection AddTransient<TService, TImplementation>(this ServiceCollection services) => services;
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert — 带参构造函数中 IEmailService 未注册仍会被报告
        result.TotalRegistrations.Should().Be(1);
        result.MissingRegistrations.Should().Contain(m =>
            m.ServiceType == "IEmailService");
    }

    #endregion
}
