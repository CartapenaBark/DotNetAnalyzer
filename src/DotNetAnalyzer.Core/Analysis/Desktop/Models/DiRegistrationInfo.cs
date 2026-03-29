namespace DotNetAnalyzer.Core.Analysis.Desktop.Models;

/// <summary>
/// DI 注册信息。
/// </summary>
public sealed class DiRegistrationInfo
{
    /// <summary>服务接口类型。</summary>
    public required string ServiceType { get; init; }

    /// <summary>实现类型。</summary>
    public required string ImplementationType { get; init; }

    /// <summary>服务生命周期。</summary>
    public required DiLifetime Lifetime { get; init; }

    /// <summary>注册所在文件。</summary>
    public required string FilePath { get; init; }

    /// <summary>注册所在行号。</summary>
    public int Line { get; init; }
}

/// <summary>
/// DI 服务生命周期。
/// </summary>
public enum DiLifetime
{
    /// <summary>Transient。</summary>
    Transient,

    /// <summary>Scoped。</summary>
    Scoped,

    /// <summary>Singleton。</summary>
    Singleton
}
