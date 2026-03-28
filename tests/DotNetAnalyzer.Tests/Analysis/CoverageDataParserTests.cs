using DotNetAnalyzer.Core.Analysis;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotNetAnalyzer.Tests.Analysis;

/// <summary>
/// CoverageDataParser 单元测试。
/// </summary>
public class CoverageDataParserTests
{
    private readonly Mock<ILogger<CoverageDataParser>> _loggerMock;
    private readonly CoverageDataParser _parser;

    public CoverageDataParserTests()
    {
        _loggerMock = new Mock<ILogger<CoverageDataParser>>();
        _parser = new CoverageDataParser(_loggerMock.Object);
    }

    /// <summary>
    /// 有效的 Cobertura XML 应正确解析所有层级数据。
    /// </summary>
    [Fact]
    public void ParseFile_ValidCoberturaXml_ShouldReturnCorrectData()
    {
        // Arrange
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.85" branch-rate="0.72" version="1.9">
              <packages>
                <package name="MyApp" line-rate="0.85" branch-rate="0.72">
                  <classes>
                    <class name="Calculator" filename="Calculator.cs"
                           line-rate="0.90" branch-rate="0.80">
                      <methods>
                        <method name="Add" line-rate="1.0" branch-rate="0.5">
                          <lines>
                            <line number="10" hits="5"
                                  branch="true"
                                  condition-coverage="50% (1/2)"/>
                            <line number="11" hits="3"/>
                          </lines>
                        </method>
                        <method name="Subtract" line-rate="0.5" branch-rate="0.0">
                          <lines>
                            <line number="15" hits="2"/>
                            <line number="16" hits="0"/>
                          </lines>
                        </method>
                      </methods>
                      <lines>
                        <line number="10" hits="5"/>
                        <line number="11" hits="3"/>
                        <line number="15" hits="2"/>
                        <line number="16" hits="0"/>
                      </lines>
                    </class>
                    <class name="Helper" filename="Helper.cs"
                           line-rate="0.50" branch-rate="0.50">
                      <methods>
                        <method name="Format" line-rate="0.5" branch-rate="1.0">
                          <lines>
                            <line number="5" hits="1"/>
                            <line number="6" hits="0"/>
                          </lines>
                        </method>
                      </methods>
                      <lines>
                        <line number="5" hits="1"/>
                        <line number="6" hits="0"/>
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, xml);

            // Act
            var result = _parser.ParseFile(tempFile);

            // Assert
            result.Should().NotBeNull();
            result!.LineRate.Should().BeApproximately(0.85, 0.001);
            result.BranchRate.Should().BeApproximately(0.72, 0.001);
            result.Files.Should().HaveCount(2);

            // 验证第一个文件
            var calcFile = result.Files[0];
            calcFile.FileName.Should().Be("Calculator.cs");
            calcFile.LineRate.Should().BeApproximately(0.90, 0.001);
            calcFile.BranchRate.Should().BeApproximately(0.80, 0.001);
            calcFile.CoveredLines.Should().Be(3); // hits > 0 的行
            calcFile.TotalLines.Should().Be(4);
            calcFile.Methods.Should().HaveCount(2);

            // 验证方法覆盖率
            var addMethod = calcFile.Methods[0];
            addMethod.MethodName.Should().Be("Add");
            addMethod.LineRate.Should().BeApproximately(1.0, 0.001);
            addMethod.CoveredLines.Should().Be(2);
            addMethod.TotalLines.Should().Be(2);

            var subMethod = calcFile.Methods[1];
            subMethod.MethodName.Should().Be("Subtract");
            subMethod.LineRate.Should().BeApproximately(0.5, 0.001);
            subMethod.CoveredLines.Should().Be(1);
            subMethod.TotalLines.Should().Be(2);

            // 验证第二个文件
            var helperFile = result.Files[1];
            helperFile.FileName.Should().Be("Helper.cs");
            helperFile.Methods.Should().HaveCount(1);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// 格式错误的 XML 应返回 null。
    /// </summary>
    [Fact]
    public void ParseFile_MalformedXml_ShouldReturnNull()
    {
        // Arrange
        var malformedXml = "<coverage><broken";
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, malformedXml);

            // Act
            var result = _parser.ParseFile(tempFile);

            // Assert
            result.Should().BeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// 非预期的根节点名称应返回 null。
    /// </summary>
    [Fact]
    public void ParseFile_WrongRootNode_ShouldReturnNull()
    {
        // Arrange
        var xml = "<report><items/></report>";
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, xml);

            // Act
            var result = _parser.ParseFile(tempFile);

            // Assert
            result.Should().BeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// 空的 coverage 节点应返回结构有效但文件列表为空的结果。
    /// </summary>
    [Fact]
    public void ParseFile_EmptyCoverage_ShouldReturnEmptyFiles()
    {
        // Arrange
        var xml = "<coverage line-rate=\"0.0\" branch-rate=\"0.0\" version=\"1.9\"/>";
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, xml);

            // Act
            var result = _parser.ParseFile(tempFile);

            // Assert
            result.Should().NotBeNull();
            result!.Files.Should().BeEmpty();
            result.LineRate.Should().Be(0.0);
            result.BranchRate.Should().Be(0.0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// 文件不存在时 ParseFile 应返回 null。
    /// </summary>
    [Fact]
    public void ParseFile_FileNotFound_ShouldReturnNull()
    {
        // Act
        var result = _parser.ParseFile(
            "/nonexistent/path/coverage.cobertura.xml");

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// 空路径应返回 null。
    /// </summary>
    [Fact]
    public void ParseFile_EmptyPath_ShouldReturnNull()
    {
        // Act
        var result = _parser.ParseFile(string.Empty);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// null 路径应返回 null。
    /// </summary>
    [Fact]
    public void ParseFile_NullPath_ShouldReturnNull()
    {
        // Act
        var result = _parser.ParseFile(null!);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// 存在的文件应正确解析。
    /// </summary>
    [Fact]
    public void ParseFile_ExistingValidFile_ShouldReturnData()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <coverage line-rate="0.75" branch-rate="0.60" version="1.9">
                  <packages>
                    <package name="Test" line-rate="0.75" branch-rate="0.60">
                      <classes>
                        <class name="Foo" filename="Foo.cs"
                               line-rate="0.75" branch-rate="0.60">
                          <methods>
                            <method name="Bar" line-rate="1.0"
                                    branch-rate="1.0">
                              <lines>
                                <line number="5" hits="3"/>
                              </lines>
                            </method>
                          </methods>
                          <lines>
                            <line number="5" hits="3"/>
                            <line number="6" hits="0"/>
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;
            File.WriteAllText(tempFile, xml);

            // Act
            var result = _parser.ParseFile(tempFile);

            // Assert
            result.Should().NotBeNull();
            result!.LineRate.Should().BeApproximately(0.75, 0.001);
            result.Files.Should().HaveCount(1);
            result.Files[0].FileName.Should().Be("Foo.cs");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// 包含无效 rate 值的 XML 应使用 0.0 作为默认值而非崩溃。
    /// </summary>
    [Fact]
    public void ParseFile_InvalidRateValue_ShouldDefaultToZero()
    {
        // Arrange
        var xml = """
            <coverage line-rate="invalid" branch-rate="abc" version="1.9">
              <packages>
                <package name="X">
                  <classes>
                    <class name="Y" filename="Y.cs"
                           line-rate="not-a-number">
                      <methods/>
                      <lines/>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, xml);

            // Act
            var result = _parser.ParseFile(tempFile);

            // Assert
            result.Should().NotBeNull();
            result!.LineRate.Should().Be(0.0);
            result.BranchRate.Should().Be(0.0);
            result.Files[0].LineRate.Should().Be(0.0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// 无 packages 包装、直接使用 classes 结构也应正常解析。
    /// </summary>
    [Fact]
    public void ParseFile_DirectClassesStructure_ShouldParse()
    {
        // Arrange
        var xml = """
            <coverage line-rate="0.50" branch-rate="0.50" version="1.9">
              <classes>
                <class name="A" filename="A.cs" line-rate="0.50"
                       branch-rate="0.50">
                  <methods/>
                  <lines>
                    <line number="1" hits="1"/>
                    <line number="2" hits="0"/>
                  </lines>
                </class>
              </classes>
            </coverage>
            """;
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, xml);

            // Act
            var result = _parser.ParseFile(tempFile);

            // Assert
            result.Should().NotBeNull();
            result!.Files.Should().HaveCount(1);
            result.Files[0].FileName.Should().Be("A.cs");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
