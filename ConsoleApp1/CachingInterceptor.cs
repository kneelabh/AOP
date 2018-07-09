using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Newtonsoft.Json;

namespace ConsoleApp1
{
    public class CachingInterceptor : IInterceptor
    {
        private readonly ICache _cache;
        private readonly ILogger<CachingInterceptor> _logger;
        private KeyGenerator _keyGenerator;

        /// <inheritdoc />
        public CachingInterceptor(ICache cache, ILogger<CachingInterceptor> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        private KeyGenerator KeyGenerator => _keyGenerator ?? (_keyGenerator = new KeyGenerator());

        private IInvocation Invocation { get; set; }

        public void Intercept(IInvocation invocation)
        {
            if (_cache.IsOperational())
            {
                if (IsAsyncMethod(invocation.Method))
                    InterceptAsync(invocation).GetAwaiter().GetResult();
                else
                    InterceptSync(invocation).GetAwaiter().GetResult();
            }
            else
            {
                invocation.Proceed();
            }
        }

        private async Task<string> CacheKey()
        {
            return await Task.FromResult(KeyGenerator.BuildCacheKey(Invocation));
        }

        private Task InterceptAsync(IInvocation invocation)
        {
            return Task.Run(() =>
            {
                Invocation = invocation;
                if (invocation.Method.ReturnType == typeof(Task))
                    PostAction(
                        ex => { LogMethod(invocation, ex); });
                else
                    PostAction(
                        invocation.Method.ReturnType.GenericTypeArguments[0],
                        ex => { LogMethod(invocation, ex); });
            });
        }

        private void LogMethod(IInvocation invocation, Exception e)
        {
            _logger.LogError($"Operation {invocation.Method.Name} threw: {e}");
        }

        private async Task InterceptSync(IInvocation invocation)
        {
            Invocation = invocation;
            var methodReturn = invocation.Method.ReturnType;
            if (methodReturn == typeof(void))
                invocation.Proceed();
            else
                await NonAsyncPostMethod();
        }

        private static bool IsAsyncMethod(MethodInfo method)
        {
            return method.ReturnType == typeof(Task) ||
                   method.ReturnType.IsGenericType &&
                   method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>);
        }

        private void PostAction(Action<Exception> finalAction)
        {
            Task.Run(() =>
            {
                Exception exception = null;
                try
                {
                    Invocation.Proceed();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                    exception = ex;
                    throw;
                }
                finally
                {
                    finalAction(exception);
                }
            });
        }

        private async Task NonAsyncPostMethod()
        {
            Exception exception = null;

            var cacheKey = await CacheKey();
            try
            {
                var storedObject = await _cache.GetObject(cacheKey, Invocation.Method.ReturnType);
                if (storedObject != null)
                {
                    Invocation.ReturnValue = storedObject;
                }
                else
                {
                    Invocation.Proceed();
                    await _cache.StoreString(cacheKey, JsonConvert.SerializeObject(Invocation.ReturnValue));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                exception = ex;
                throw;
            }
            finally
            {
                LogMethod(Invocation, exception);
            }
        }

        /// <summary>
        ///     Called Through Reflection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="finalAction"></param>
        /// <returns></returns>
        public async Task AsyncPostMethod<T>(Action<Exception> finalAction)
        {
            Exception exception = null;
            var cacheKey = await CacheKey();
            try
            {
                var task = (Task<T>) _cache.GetType()
                    .GetMethod("GetTypedObject", new[] {typeof(string)})
                    .MakeGenericMethod(typeof(T))
                    .Invoke(_cache, new object[] {cacheKey});

                var storedObject = task.Result;
                if (storedObject != null && EqualityComparer<T>.Default.Equals(storedObject, default) == false)
                {
                    Invocation.ReturnValue = task;
                }
                else
                {
                    Invocation.Proceed();
                    var methodReturn = await (Task<T>) Invocation.ReturnValue;
                    await _cache.StoreString(cacheKey, JsonConvert.SerializeObject(methodReturn));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                exception = ex;
                throw;
            }
            finally
            {
                finalAction(exception);
            }
        }

        private void PostAction(Type taskReturnType, Action<Exception> finalAction)
        {
            typeof(CachingInterceptor)
                .GetMethod("AsyncPostMethod")
                .MakeGenericMethod(taskReturnType)
                .Invoke(this, new object[] {finalAction});
        }
    }
}