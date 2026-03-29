// SEC005: 路径遍历测试样本
using System.IO;

namespace SecurityTestAssets.PathTraversal
{
    public class BadExamples
    {
        public byte[] ReadUserFile(string userFileName)
        {
            // SEC005: Path.Combine 结果未验证直接传入文件操作
            var path = Path.Combine("/uploads", userFileName);
            return File.ReadAllBytes(path);
        }

        public Stream GetLogFile(string logName)
        {
            // SEC005: 同样的问题
            var filePath = Path.Combine("/var/log", logName);
            return File.OpenRead(filePath);
        }
    }

    public class GoodExamples
    {
        public byte[] ReadUserFile(string userFileName)
        {
            var basePath = "/uploads";
            var fullPath = Path.GetFullPath(Path.Combine(basePath, userFileName));

            // 验证路径在允许的根目录下
            if (!fullPath.StartsWith(Path.GetFullPath(basePath)))
            {
                throw new UnauthorizedAccessException("Path traversal detected");
            }

            return File.ReadAllBytes(fullPath);
        }
    }
}
