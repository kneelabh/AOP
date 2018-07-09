using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Castle.DynamicProxy;

namespace ConsoleApp1
{
    public class KeyGenerator :  IKeyGenerator
    {
        private ParameterInfo[] _methodParameters;

        private Dictionary<int, string> _parametersNameValueMapper;

        public KeyGenerator()
        {
            CacheKeyBuilder = new StringBuilder();
        }

        public KeyGenerator(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
                ClassName = className;
            CacheKeyBuilder = new StringBuilder();
        }

        public KeyGenerator(string className, string methodName)
        {
            ClassName = className;
            MethodName = methodName;
            CacheKeyBuilder = new StringBuilder();
        }

        private static StringBuilder CacheKeyBuilder { get; set; }

        public string ClassName { get; set; }

        public string MethodName { get; set; }

        public ParameterInfo[] MethodParameters
        {
            get => _methodParameters;
            set
            {
                _methodParameters = value;
                NameValueMapper(_methodParameters);
            }
        }

        public string ParameterProperty { get; set; }

        public CacheSettings Settings { get; set; }

        public string BuildCacheKey(object[] arguments)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ClassName) == false)
                    CacheKeyBuilder.Append(ClassName);
                if (string.IsNullOrWhiteSpace(MethodName) == false)
                    CacheKeyBuilder.Append(MethodName);
                GetArguments(arguments);

                using (var provider = new SHA1CryptoServiceProvider())
                {
                    var bytes = Encoding.UTF8.GetBytes(CacheKeyBuilder.ToString());
                    var computeHash = provider.ComputeHash(bytes);
                    return computeHash.Length == 0
                        ? string.Empty
                        : Convert.ToBase64String(computeHash);
                }
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }

        public string BuildCacheKey(IInvocation invocation)
        {
            MethodName = invocation.Method.Name;
            ClassName = invocation.TargetType.Name;
            return BuildCacheKey(invocation.Arguments);
        }

        private void GetArguments(IReadOnlyList<object> arguments)
        {
            int argIndex;
            switch (Settings)
            {
                case CacheSettings.IgnoreParameters:
                    return;
                case CacheSettings.UseId:
                    argIndex = GetArgumentIndexByName("Id");
                    CacheKeyBuilder.Append(arguments[argIndex] ?? "Null");
                    break;
                case CacheSettings.UseProperty:
                    argIndex = GetArgumentIndexByName(ParameterProperty);
                    CacheKeyBuilder.Append(arguments[argIndex] ?? "Null");
                    break;
                case CacheSettings.Default:
                    GenerateKeyByArguments(arguments);
                    break;
            }
        }

        private static void GenerateKeyByArguments(IEnumerable<object> arguments)
        {
            foreach (var (value, index) in arguments.ToList().WithIndex())
                if (value.GetType().IsPrimitive)
                {
                    CacheKeyBuilder.Append($"{index}:{value}:{value}_");
                }
                else if (value.GetType().IsGenericType)
                {
                    CacheKeyBuilder.Append($"{index}_");
                    switch (value)
                    {
                        case IList list:
                            var breakPoint = Math.Ceiling((double) list.Count / 10);
                            foreach (var element in list)
                            {
                                CacheKeyBuilder.Append($"{element}_");
                                if (list.Count > 5 && Math.Abs(--breakPoint) < 0) break;
                            }

                            break;
                        case IDictionary m:
                            CacheKeyBuilder.Append(
                                $"{value.GetType().GetGenericArguments()[0].Name}_{value.GetType().GetGenericArguments()[1].Name}");
                            breakPoint = Math.Ceiling((double) m.Count / 10);
                            foreach (DictionaryEntry dictionaryEntry in m)
                            {
                                CacheKeyBuilder.Append($"{dictionaryEntry.Key}:{dictionaryEntry.Value}");
                                if (m.Count > 5 && Math.Abs(--breakPoint) < 0) break;
                            }

                            break;
                        case IEnumerable enumerable:
                            foreach (var element in enumerable) CacheKeyBuilder.Append($"{element}   ");
                            break;
                        default:
                            CacheKeyBuilder.Append($"{index}:{value.GetHashCode().ToString()}");
                            break;
                    }
                }
                else if (value is CancellationToken)
                {
                    return;
                }
                else
                {
                    CacheKeyBuilder.Append($"{index}:{value}");
                }
        }

        public string BuildDefaultKey(object[] arguments)
        {
            if (arguments != null)
                GenerateKeyByArguments(arguments);
            else
                CacheKeyBuilder.Append(arguments?.ToString() ?? "Null");
            return BuildCacheKey(arguments);
        }

        private int GetArgumentIndexByName(string paramName)
        {
            var paramKeyValue =
                _parametersNameValueMapper.SingleOrDefault(
                    arg =>
                        string.Compare(arg.Value, paramName, CultureInfo.InvariantCulture, CompareOptions.IgnoreCase) ==
                        0);
            return paramKeyValue.Key;
        }

        private void NameValueMapper(IReadOnlyList<ParameterInfo> methodParameters)
        {
            _parametersNameValueMapper = new Dictionary<int, string>();
            for (var i = 0; i < methodParameters.Count(); i++)
                _parametersNameValueMapper.Add(i, methodParameters[i].Name);
        }
    }
}