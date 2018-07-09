using System.Reflection;
using Castle.DynamicProxy;

namespace ConsoleApp1
{
    public interface IKeyGenerator
    {
        string ClassName { get; set; }
        string MethodName { get; set; }
        ParameterInfo[] MethodParameters { get; set; }
        string ParameterProperty { get; set; }
        CacheSettings Settings { get; set; }

        string BuildCacheKey(IInvocation invocation);
        string BuildCacheKey(object[] arguments);
        string BuildDefaultKey(object[] arguments);
    }
}