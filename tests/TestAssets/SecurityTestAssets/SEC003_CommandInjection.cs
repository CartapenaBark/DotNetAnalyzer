// SEC003: 命令注入测试样本
using System.Diagnostics;

namespace SecurityTestAssets.CommandInjection
{
    public class BadExamples
    {
        public void RunCommand(string userInput, string fileName)
        {
            // SEC003: Process.Start 使用字符串拼接
            Process.Start("cmd", "/c " + userInput);
            Process.Start("bash", "-c " + "cat " + fileName);
        }
    }

    public class GoodExamples
    {
        public void RunCommandSafely(string fileName)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cat",
                Arguments = fileName,
                UseShellExecute = false
            };
            // 验证 fileName 在允许列表中
            Process.Start(psi);
        }
    }
}
