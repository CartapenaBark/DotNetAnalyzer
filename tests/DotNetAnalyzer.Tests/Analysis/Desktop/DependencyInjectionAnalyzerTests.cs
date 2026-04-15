using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DotNetAnalyzer.Core.Analysis.Desktop;
using DotNetAnalyzer.Core.Analysis.Desktop.Models;
using DotNetAnalyzer.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace DotNetAnalyzer.Tests.Analysis.Desktop;

/// <summary>
/// DependencyInjectionAnalyzer 单元测试。
/// </summary>
/// <remarks>
/// 覆盖 DI 注册收集、lambda 工厂注册、开放泛型匹配、
/// Captive Dependency（DI004）、循环依赖（DI005）和配置开关。
/// </remarks>
public class DependencyInjectionAnalyzerTests
{
    private readonly DependencyInjectionAnalyzer _analyzer;

    public DependencyInjectionAnalyzerTests()
    {
        _analyzer = new DependencyInjectionAnalyzer(
            NullLogger<DependencyInjectionAnalyzer>.Instance,
            Options.Create(new AnalyzerOptions()));
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

        // Assert
        result.TotalMissing.Should().Be(0);
        result.MissingRegistrations.Should().BeEmpty();
    }

    #endregion

    #region 边界情况

    [Fact]
    public async Task AnalyzeAsync_EmptyProject_ReturnsEmptyResults()
    {
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

        // Assert
        result.TotalRegistrations.Should().Be(1);
        result.MissingRegistrations.Should().Contain(m =>
            m.ServiceType == "IEmailService");
    }

    #endregion

    #region Lambda 工厂方法注册

    [Fact]
    public async Task AnalyzeAsync_LambdaObjectCreation_RegistersImplementation()
    {
        // Arrange
        // services.AddSingleton<IFoo>(sp => new FooImpl())
        var source = """
            using System;

            public class Startup
            {
                public void ConfigureService(IServiceCollection services)
                {
                    services.AddSingleton<IFoo>(sp => new FooImpl());
                }
            }

            public interface IFoo { }
            public class FooImpl : IFoo { }
            public class ServiceCollection { }
            public static class ServiceCollectionExtensions
            {
                public static ServiceCollection AddSingleton<TService>(this ServiceCollection s, Func<IServiceProvider, TService> f) => s;
                public static ServiceCollection AddScoped<TService>(this ServiceCollection s, Func<IServiceProvider, TService> f) => s;
                public static ServiceCollection AddTransient<TService>(this ServiceCollection s, Func<IServiceProvider, TService> f) => s;
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert
        result.TotalRegistrations.Should().Be(1);
        result.Registrations[0].ServiceType.Should().Be("IFoo");
        result.Registrations[0].ImplementationType.Should().Be("FooImpl");
        result.Registrations[0].Lifetime.Should().Be(DiLifetime.Singleton);
    }

    [Fact]
    public async Task AnalyzeAsync_LambdaBlockBody_ReturnCreation_RegistersImplementation()
    {
        // Arrange
        // services.AddScoped<IBar>(sp => { return new BarImpl(); })
        var source = """
            using System;

            public class Startup
            {
                public void ConfigureService(IServiceCollection services)
                {
                    services.AddScoped<IBar>(sp => { return new BarImpl(); });
                }
            }

            public interface IBar { }
            public class BarImpl : IBar { }
            public class ServiceCollection { }
            public static class ServiceCollectionExtensions
            {
                public static ServiceCollection AddSingleton<TService>(this ServiceCollection s, Func<IServiceProvider, TService> f) => s;
                public static ServiceCollection AddScoped<TService>(this ServiceCollection s, Func<IServiceProvider, TService> f) => s;
                public static ServiceCollection AddTransient<TService>(this ServiceCollection s, Func<IServiceProvider, TService> f) => s;
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert
        result.TotalRegistrations.Should().Be(1);
        result.Registrations[0].ImplementationType.Should().Be("BarImpl");
        result.Registrations[0].Lifetime.Should().Be(DiLifetime.Scoped);
    }

    #endregion

    #region 开放泛型注册检测

    [Fact]
    public async Task AnalyzeAsync_OpenGenericRegistration_MarksAsOpenGeneric()
    {
        // Arrange
        // services.AddScoped(typeof(IRepository<>), typeof(Repository<>))
        var source = """
            public class Startup
            {
                public void ConfigureService(IServiceCollection services)
                {
                    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
                }
            }

            public interface IRepository<T> { }
            public class Repository<T> : IRepository<T> { }
            public class ServiceCollection { }
            public static class ServiceCollectionExtensions
            {
                public static ServiceCollection AddSingleton(this ServiceCollection s, Type t1, Type t2) => s;
                public static ServiceCollection AddScoped(this ServiceCollection s, Type t1, Type t2) => s;
                public static ServiceCollection AddTransient(this ServiceCollection s, Type t1, Type t2) => s;
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert
        result.TotalRegistrations.Should().Be(1);
        result.Registrations[0].ServiceType.Should().Contain("IRepository<>");
        result.Registrations[0].IsOpenGeneric.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_ClosedGenericMatchesOpenGeneric_NoMissing()
    {
        // Arrange
        // 开放泛型注册 typeof(IRepository<>)，OrderService 构造函数需要 IRepository<Order>
        var source = """
            public class Startup
            {
                public void ConfigureService(IServiceCollection services)
                {
                    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
                    services.AddScoped<OrderService>();
                }
            }

            public interface IRepository<T> { }
            public class Repository<T> : IRepository<T> { }
            public class Order { }
            public class OrderService
            {
                public OrderService(IRepository<Order> orderRepo) { }
            }
            public class ServiceCollection { }
            public static class ServiceCollectionExtensions
            {
                public static ServiceCollection AddSingleton(this ServiceCollection s, Type t1, Type t2) => s;
                public static ServiceCollection AddScoped(this ServiceCollection s, Type t1, Type t2) => s;
                public static ServiceCollection AddTransient(this ServiceCollection s, Type t1, Type t2) => s;
                public static ServiceCollection AddScoped<TService>(this ServiceCollection s) => s;
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert — IRepository<Order> 应匹配开放泛型 IRepository<>
        result.TotalMissing.Should().Be(0);
    }

    #endregion

    #region Captive Dependency（DI004）

    [Fact]
    public async Task AnalyzeAsync_SingletonDependsOnScoped_DetectsCaptive()
    {
        // Arrange
        // Singleton MySingletonService 依赖 Scoped MyScopedService
        var source = """
            public class Startup
            {
                public void ConfigureService(IServiceCollection services)
                {
                    services.AddSingleton<MySingletonService>();
                    services.AddScoped<MyScopedService>();
                }
            }

            public class MySingletonService
            {
                public MySingletonService(MyScopedService scoped) { }
            }
            public class MyScopedService { }
            public class ServiceCollection { }
            public static class ServiceCollectionExtensions
            {
                public static ServiceCollection AddSingleton<TService>(this ServiceCollection s) => s;
                public static ServiceCollection AddScoped<TService>(this ServiceCollection s) => s;
                public static ServiceCollection AddTransient<TService>(this ServiceCollection s) => s;
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert
        result.CaptiveDependencies.Should().Contain(c =>
            c.HolderType == "MySingletonService" &&
            c.CapturedDependency == "MyScopedService");
    }

    [Fact]
    public async Task AnalyzeAsync_SingletonDependsOnSingleton_NoCaptive()
    {
        // Arrange
        // Singleton 依赖 Singleton — 不是 Captive Dependency
        var source = """
            public class Startup
            {
                public void ConfigureService(IServiceCollection services)
                {
                    services.AddSingleton<ServiceA>();
                    services.AddSingleton<ServiceB>();
                }
            }

            public class ServiceA
            {
                public ServiceA(ServiceB b) { }
            }
            public class ServiceB { }
            public class ServiceCollection { }
            public static class ServiceCollectionExtensions
            {
                public static ServiceCollection AddSingleton<TService>(this ServiceCollection s) => s;
                public static ServiceCollection AddScoped<TService>(this ServiceCollection s) => s;
                public static ServiceCollection AddTransient<TService>(this ServiceCollection s) => s;
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert
        result.CaptiveDependencies.Should().BeEmpty();
    }

    #endregion

    #region 循环依赖检测（DI005）

    [Fact]
    public async Task AnalyzeAsync_CircularDependency_DetectsCycle()
    {
        // Arrange
        // ServiceA → ServiceB → ServiceA（循环）
        var source = """
            public class Startup
            {
                public void ConfigureService(IServiceCollection services)
                {
                    services.AddSingleton<ServiceA>();
                    services.AddSingleton<ServiceB>();
                }
            }

            public class ServiceA
            {
                public ServiceA(ServiceB b) { }
            }
            public class ServiceB
            {
                public ServiceB(ServiceA a) { }
            }
            public class ServiceCollection { }
            public static class ServiceCollectionExtensions
            {
                public static ServiceCollection AddSingleton<TService>(this ServiceCollection s) => s;
                public static ServiceCollection AddScoped<TService>(this ServiceCollection s) => s;
                public static ServiceCollection AddTransient<TService>(this ServiceCollection s) => s;
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert
        result.CircularDependencies.Should().NotBeEmpty();
        var cycle = result.CircularDependencies[0];
        cycle.DependencyChain.Should().Contain("ServiceA");
        cycle.DependencyChain.Should().Contain("ServiceB");
    }

    [Fact]
    public async Task AnalyzeAsync_NoCircularDependency_NoCycle()
    {
        // Arrange
        // ServiceA → ServiceB → ServiceC（单向链，无循环）
        var source = """
            public class Startup
            {
                public void ConfigureService(IServiceCollection services)
                {
                    services.AddSingleton<ServiceA>();
                    services.AddSingleton<ServiceB>();
                    services.AddSingleton<ServiceC>();
                }
            }

            public class ServiceA
            {
                public ServiceA(ServiceB b) { }
            }
            public class ServiceB
            {
                public ServiceB(ServiceC c) { }
            }
            public class ServiceC { }
            public class ServiceCollection { }
            public static class ServiceCollectionExtensions
            {
                public static ServiceCollection AddSingleton<TService>(this ServiceCollection s) => s;
                public static ServiceCollection AddScoped<TService>(this ServiceCollection s) => s;
                public static ServiceCollection AddTransient<TService>(this ServiceCollection s) => s;
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert
        result.CircularDependencies.Should().BeEmpty();
    }

    #endregion

    #region 配置开关

    [Fact]
    public async Task AnalyzeAsync_CaptiveDependencyDisabled_NoCaptiveResults()
    {
        // Arrange
        var options = new AnalyzerOptions
        {
            Di = new DiOptions { CaptiveDependency = false }
        };

        var analyzer = new DependencyInjectionAnalyzer(
            NullLogger<DependencyInjectionAnalyzer>.Instance,
            Options.Create(options));

        var source = """
            public class Startup
            {
                public void ConfigureService(IServiceCollection services)
                {
                    services.AddSingleton<MySingletonService>();
                    services.AddScoped<MyScopedService>();
                }
            }

            public class MySingletonService
            {
                public MySingletonService(MyScopedService scoped) { }
            }
            public class MyScopedService { }
            public class ServiceCollection { }
            public static class ServiceCollectionExtensions
            {
                public static ServiceCollection AddSingleton<TService>(this ServiceCollection s) => s;
                public static ServiceCollection AddScoped<TService>(this ServiceCollection s) => s;
                public static ServiceCollection AddTransient<TService>(this ServiceCollection s) => s;
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var result = await analyzer.AnalyzeAsync(project);

        // Assert — Captive Dependency 检测已禁用
        result.CaptiveDependencies.Should().BeEmpty();
    }

    #endregion
}
