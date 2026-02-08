using System.Text;

var test = "Content-Length: 157\r\n\r\n{}";
Console.OutputEncoding = new UTF8Encoding(false);
Console.OpenStandardOutput().Write(Encoding.UTF8.GetBytes(test), 0, test.Length);
