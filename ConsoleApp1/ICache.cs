using System;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    public interface ICache
    {
        bool IsOperational();
        Task<object> GetObject(string cacheKey, Type methodReturnType);
        Task<T> GetTypedObject<T>(string cacheKey);
        Task StoreString(string cacheKey, string serializeObject);
    }
}