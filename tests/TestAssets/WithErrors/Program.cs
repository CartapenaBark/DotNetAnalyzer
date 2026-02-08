using System;

namespace WithErrors;

// 这个类包含各种编译错误，用于测试诊断功能
class ErrorTestClass
{
    // 错误1：未使用的变量
    private int unusedVariable = 42;

    // 错误2：方法参数未使用
    public void UnusedParameter(int param)
    {
        Console.WriteLine("Test");
    }

    // 错误3：可能的空引用
    public string? PossibleNullReturn()
    {
        return null;
    }

    public void CallPossibleNull()
    {
        var result = PossibleNullReturn();
        var length = result.Length; // 可能为空
    }

    // 错误4：类型转换可能失败
    public void UnsafeCast(object obj)
    {
        var str = (string)obj; // 可能抛出 InvalidCastException
    }

    // 错误5：async 方法缺少 await
    public async Task AsyncWithoutAwait()
    {
        Console.WriteLine("No await here");
    }

    // 错误6：未实现的方法
    public abstract class AbstractClass
    {
        public abstract void AbstractMethod();
    }

    public class ConcreteClass : AbstractClass
    {
        // 未实现抽象方法 - 编译错误
    }
}

// 错误7：使用过时的 API
class ObsoleteApiUsage
{
    [Obsolete("This method is obsolete")]
    public void OldMethod() { }

    public void UseObsolete()
    {
        OldMethod();
    }
}
