using System.Collections.Generic;
using System.Linq;

namespace ConsoleApp1
{
    internal static class Extension
    {
        public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self)
        {
            return self.Select((item, index) => (item, index));
        }
    }
}