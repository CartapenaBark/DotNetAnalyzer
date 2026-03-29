// SEC001: 硬编码凭据测试样本
namespace SecurityTestAssets.HardcodedCredentials
{
    public class BadExamples
    {
        public void StoreCredentials()
        {
            // SEC001: 硬编码密码
            string password = "SuperSecret123!";
            string apiKey = "sk-abc123def456";
            string connectionString = "Server=localhost;Password=admin123;Database=mydb";
            string secret = "my-super-secret-value";
            string pwd = "P@ssw0rd!";
        }

        [Authorize("hardcoded-secret-key")]
        public void SecuredEndpoint() { }
    }

    public class GoodExamples
    {
        public void LoadCredentials(IConfiguration config)
        {
            // 安全: 从配置读取
            var password = config["DbPassword"];
            var apiKey = Environment.GetEnvironmentVariable("API_KEY");
            var connectionString = config.GetConnectionString("DefaultConnection");
        }
    }
}
