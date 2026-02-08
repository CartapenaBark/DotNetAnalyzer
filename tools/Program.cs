using System.Diagnostics;
using System.Text;

var toolPath = @"d:\Documents\Visual Studio Code\Workspace\DotNetAnalyzer\.tools";
var toolExe = Path.Combine(toolPath, "dotnet-analyzer.exe");
var toolDll = Path.Combine(toolPath, "dotnet-analyzer.dll");

// 选择可执行的文件
var fileName = File.Exists(toolExe) ? toolExe :
               File.Exists(toolDll) ? "dotnet" :
               throw new FileNotFoundException("Tool not found");

var arguments = File.Exists(toolDll) ? $"\"{toolDll}\" mcp serve" : "mcp serve";

Console.WriteLine("=== MCP Server Test ===");
Console.WriteLine($"Tool: {fileName}");
Console.WriteLine($"Args: {arguments}");
Console.WriteLine();

var startInfo = new ProcessStartInfo
{
    FileName = fileName,
    Arguments = arguments,
    UseShellExecute = false,
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true
};

using var process = new Process { StartInfo = startInfo };

try
{
    process.Start();

    // Build MCP request
    var json = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1.0\"}}}";
    var bytes = Encoding.UTF8.GetBytes(json);
    var request = $"Content-Length: {bytes.Length}\r\n\r\n{json}";

    Console.WriteLine($"Sending request ({bytes.Length} bytes)...");
    process.StandardInput.Write(request);
    process.StandardInput.Flush();

    // Read response
    Thread.Sleep(500);

    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();

    Console.WriteLine();
    Console.WriteLine("=== Server Output ===");
    Console.WriteLine(output);

    if (!string.IsNullOrEmpty(error))
    {
        Console.WriteLine();
        Console.WriteLine("=== Server Errors ===");
        Console.WriteLine(error);
    }

    // Parse response
    if (output.Contains("Content-Length:"))
    {
        var parts = output.Split(new[] { "\r\n\r\n" }, StringSplitOptions.None);
        if (parts.Length >= 2)
        {
            var responseJson = parts[1].Trim();
            Console.WriteLine();
            Console.WriteLine("=== Parsed Response ===");
            Console.WriteLine(responseJson);
        }
    }
}
finally
{
    if (!process.HasExited)
    {
        process.Kill();
        process.WaitForExit();
    }
}

Console.WriteLine();
Console.WriteLine("Test completed.");
