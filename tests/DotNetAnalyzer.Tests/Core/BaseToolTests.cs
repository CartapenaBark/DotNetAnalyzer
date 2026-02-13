using System.Text.Json;
using DotNetAnalyzer.Cli.Tools;
using FluentAssertions;
using Xunit;

namespace DotNetAnalyzer.Tests.Core;

/// <summary>
/// BaseTool 基类的单元测试
/// </summary>
public class BaseToolTests
{
    public class CreateErrorResponse
    {
        [Fact]
        public void 应该创建包含_success_false_的错误响应()
        {
            // Arrange
            var errorMessage = "测试错误消息";

            // Act
            var response = BaseTool.CreateErrorResponse(errorMessage);

            // Assert
            response.Should().NotBeNull();
            var doc = JsonDocument.Parse(response);
            doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
            doc.RootElement.GetProperty("error").GetString().Should().Be(errorMessage);
        }

        [Fact]
        public void 应该正确转义特殊字符()
        {
            // Arrange
            var errorMessage = "错误包含\"引号\"和\n换行";

            // Act
            var response = BaseTool.CreateErrorResponse(errorMessage);

            // Assert
            response.Should().NotBeNull();

            // 验证 JSON 可以被解析
            var doc = JsonDocument.Parse(response);
            doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
            doc.RootElement.GetProperty("error").GetString().Should().Be(errorMessage);
        }

        [Fact]
        public void 空错误消息也应该生成有效JSON()
        {
            // Arrange
            var emptyMessage = string.Empty;

            // Act
            var response = BaseTool.CreateErrorResponse(emptyMessage);

            // Assert
            response.Should().NotBeNull();
            var doc = JsonDocument.Parse(response);
            doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
            doc.RootElement.GetProperty("error").GetString().Should().BeEmpty();
        }

        [Fact]
        public void 应该符合项目约定的JSON格式()
        {
            // Arrange
            var errorMessage = "测试错误";

            // Act
            var response = BaseTool.CreateErrorResponse(errorMessage);

            // Assert
            var doc = JsonDocument.Parse(response);

            // 验证必须包含 success 和 error 字段
            doc.RootElement.TryGetProperty("success", out var successProp).Should().BeTrue();
            doc.RootElement.TryGetProperty("error", out var errorProp).Should().BeTrue();

            // 验证没有其他多余字段
            var elementCount = 0;
            using (var enumerator = doc.RootElement.EnumerateObject())
            {
                while (enumerator.MoveNext())
                {
                    elementCount++;
                }
            }
            elementCount.Should().Be(2);
        }
    }

    public class ValidateFileExists
    {
        [Fact]
        public void 文件存在时应该返回null()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();

            try
            {
                // Act
                var error = BaseTool.ValidateFileExists(tempFile);

                // Assert
                error.Should().BeNull();
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void 文件不存在时应该返回错误响应()
        {
            // Arrange
            var nonExistentFile = "C:/不存在的/文件/path.cs";

            // Act
            var error = BaseTool.ValidateFileExists(nonExistentFile);

            // Assert
            error.Should().NotBeNull();
            error.Should().Contain("\"success\":false");
            error.Should().Contain("文件不存在");
        }

        [Fact]
        public void 应该使用自定义错误消息()
        {
            // Arrange
            var nonExistentFile = "C:/不存在的/文件/path.cs";
            var customMessage = "自定义错误: 文件未找到";

            // Act
            var error = BaseTool.ValidateFileExists(nonExistentFile, customMessage);

            // Assert
            error.Should().NotBeNull();
            error.Should().Contain(customMessage);
        }
    }

    public class ValidatePathExists
    {
        [Fact]
        public void 路径存在时应该返回null()
        {
            // Arrange
            var tempPath = Path.GetTempPath();

            // Act
            var error = BaseTool.ValidatePathExists(tempPath);

            // Assert
            error.Should().BeNull();
        }

        [Fact]
        public void 路径不存在时应该返回错误响应()
        {
            // Arrange
            var nonExistentPath = "C:/不存在的/路径/";

            // Act
            var error = BaseTool.ValidatePathExists(nonExistentPath);

            // Assert
            error.Should().NotBeNull();
            error.Should().Contain("\"success\":false");
            error.Should().Contain("路径不存在");
        }
    }
}
