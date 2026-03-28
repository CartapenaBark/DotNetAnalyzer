using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Analysis;

/// <summary>
/// Cobertura XML 覆盖率文件解析器。
/// 解析 dotnet test --collect:"XPlat Code Coverage" 生成的 coverage.cobertura.xml 文件，
/// 提取逐文件、逐方法的行覆盖率和分支覆盖率。
/// </summary>
public partial class CoverageDataParser
{
    private readonly ILogger<CoverageDataParser> _logger;

    [LoggerMessage(LogLevel.Debug, "覆盖率文件路径为空，跳过解析。")]
    private static partial void LogEmptyFilePath(ILogger logger);

    [LoggerMessage(LogLevel.Debug, "覆盖率文件不存在: {FilePath}")]
    private static partial void LogFileNotFound(ILogger logger, string filePath);

    [LoggerMessage(LogLevel.Warning, "读取覆盖率文件失败: {FilePath}")]
    private static partial void LogFileReadFailed(
        ILogger logger, Exception ex, string filePath);

    [LoggerMessage(LogLevel.Warning, "覆盖率 XML 格式错误，无法解析。")]
    private static partial void LogXmlParseError(ILogger logger, Exception ex);

    [LoggerMessage(LogLevel.Debug, "覆盖率 XML 根节点不是 <coverage>，跳过解析。")]
    private static partial void LogInvalidRootNode(ILogger logger);

    /// <summary>
    /// 初始化 <see cref="CoverageDataParser"/> 的新实例。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    public CoverageDataParser(ILogger<CoverageDataParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 从指定文件路径解析 Cobertura XML 覆盖率数据。
    /// </summary>
    /// <param name="filePath">coverage.cobertura.xml 文件的绝对路径。</param>
    /// <returns>
    /// 解析成功时返回 <see cref="CoverageData"/>；
    /// 文件不存在或格式错误时返回 <c>null</c>。
    /// </returns>
    public CoverageData? ParseFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            LogEmptyFilePath(_logger);
            return null;
        }

        if (!File.Exists(filePath))
        {
            LogFileNotFound(_logger, filePath);
            return null;
        }

        try
        {
            var xml = File.ReadAllText(filePath);
            return ParseXml(xml);
        }
        catch (Exception ex)
        {
            LogFileReadFailed(_logger, ex, filePath);
            return null;
        }
    }

    /// <summary>
    /// 解析 Cobertura XML 字符串并返回结构化覆盖率数据。
    /// </summary>
    /// <param name="xml">Cobertura XML 内容。</param>
    /// <returns>解析成功时返回 <see cref="CoverageData"/>；格式错误时返回 <c>null</c>。</returns>
    internal CoverageData? ParseXml(string xml)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            LogXmlParseError(_logger, ex);
            return null;
        }

        var coverageElement = doc.Root;
        if (coverageElement == null ||
            coverageElement.Name.LocalName != "coverage")
        {
            LogInvalidRootNode(_logger);
            return null;
        }

        var lineRate = ParseRate(coverageElement.Attribute("line-rate")?.Value);
        var branchRate = ParseRate(coverageElement.Attribute("branch-rate")?.Value);

        var files = new List<FileCoverageData>();

        // 遍历 packages > package > classes > class
        var classes = coverageElement
            .Elements("packages")
            .Elements("package")
            .Elements("classes")
            .Elements("class");

        foreach (var classElement in classes)
        {
            var fileCoverage = CoverageDataParser.ParseClassElement(classElement);
            if (fileCoverage != null)
            {
                files.Add(fileCoverage);
            }
        }

        // 也支持没有 packages 包装的 <classes><class> 结构
        if (files.Count == 0)
        {
            var directClasses = coverageElement
                .Elements("classes")
                .Elements("class");

            foreach (var classElement in directClasses)
            {
                var fileCoverage = CoverageDataParser.ParseClassElement(classElement);
                if (fileCoverage != null)
                {
                    files.Add(fileCoverage);
                }
            }
        }

        return new CoverageData
        {
            LineRate = lineRate,
            BranchRate = branchRate,
            Files = files
        };
    }

    /// <summary>
    /// 解析单个 class 元素，提取文件级和方法级覆盖率。
    /// </summary>
    private static FileCoverageData? ParseClassElement(XElement classElement)
    {
        var fileName = classElement.Attribute("filename")?.Value;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var classLineRate = ParseRate(
            classElement.Attribute("line-rate")?.Value);
        var classBranchRate = ParseRate(
            classElement.Attribute("branch-rate")?.Value);

        var methods = new List<MethodCoverageData>();

        // 解析方法级覆盖率
        var methodElements = classElement
            .Elements("methods")
            .Elements("method");

        foreach (var methodElement in methodElements)
        {
            var methodName = methodElement.Attribute("name")?.Value ?? "unknown";
            var methodLineRate = ParseRate(
                methodElement.Attribute("line-rate")?.Value);
            var methodBranchRate = ParseRate(
                methodElement.Attribute("branch-rate")?.Value);

            var methodLines = methodElement
                .Elements("lines")
                .Elements("line");

            var coveredLines = 0;
            var totalLines = 0;

            foreach (var lineElement in methodLines)
            {
                var hitsAttr = lineElement.Attribute("hits")?.Value;
                if (int.TryParse(hitsAttr, out var hits))
                {
                    totalLines++;
                    if (hits > 0)
                    {
                        coveredLines++;
                    }
                }
            }

            methods.Add(new MethodCoverageData
            {
                MethodName = methodName,
                LineRate = methodLineRate,
                BranchRate = methodBranchRate,
                CoveredLines = coveredLines,
                TotalLines = totalLines
            });
        }

        // 解析文件级行信息（class/lines）
        var classLines = classElement
            .Elements("lines")
            .Elements("line");

        var fileCoveredLines = 0;
        var fileTotalLines = 0;

        foreach (var lineElement in classLines)
        {
            var hitsAttr = lineElement.Attribute("hits")?.Value;
            if (int.TryParse(hitsAttr, out var hits))
            {
                fileTotalLines++;
                if (hits > 0)
                {
                    fileCoveredLines++;
                }
            }
        }

        return new FileCoverageData
        {
            FileName = fileName,
            LineRate = classLineRate,
            BranchRate = classBranchRate,
            CoveredLines = fileCoveredLines,
            TotalLines = fileTotalLines,
            Methods = methods
        };
    }

    /// <summary>
    /// 安全解析覆盖率比率值（0.0 ~ 1.0）。
    /// </summary>
    private static double ParseRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0.0;
        }

        if (double.TryParse(value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var rate))
        {
            return Math.Clamp(rate, 0.0, 1.0);
        }

        return 0.0;
    }
}
