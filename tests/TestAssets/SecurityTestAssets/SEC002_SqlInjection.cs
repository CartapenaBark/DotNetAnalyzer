// SEC002: SQL 注入测试样本
using System.Data.SqlClient;

namespace SecurityTestAssets.SqlInjection
{
    public class BadExamples
    {
        public void VulnerableSql(string userId, string tableName)
        {
            // SEC002: 字符串拼接 SQL
            string sql = "SELECT * FROM users WHERE id = " + userId;
            string query = "DELETE FROM " + tableName + " WHERE active = 0";

            // SEC002: 字符串插值 SQL
            string interpolated = $"SELECT * FROM products WHERE name = '{userId}'";
        }

        public void SqlCommandVulnerable(string userInput)
        {
            using var cmd = new SqlCommand();
            // SEC002: CommandText 使用拼接
            cmd.CommandText = "SELECT * FROM users WHERE name = '" + userInput + "'";
        }
    }

    public class GoodExamples
    {
        public void ParameterizedQuery(string userId)
        {
            using var cmd = new SqlCommand();
            cmd.CommandText = "SELECT * FROM users WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", userId);
        }
    }
}
