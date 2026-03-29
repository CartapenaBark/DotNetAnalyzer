// SEC006: XSS 测试样本 (需要引用 AspNetCore 才会激活)
// 此文件仅包含 AspNetCore 相关检测的示例代码

namespace SecurityTestAssets.XssInAspNet
{
    // 在引用 AspNetCore 的项目中:
    // @Html.Raw(userComment) 会被检测到
    // Response.Write(userInput) 会被检测到
}
