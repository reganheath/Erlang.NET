using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Erlang.NET
{
    internal static class Deconstuct
    {
        public static void Deconstruct<T>(this T[] array, out T o1)
        {
            o1 = (array.Length > 0 ? array[0] : default);
        }

        public static void Deconstruct<T>(this T[] array, out T o1, out T o2)
        {
            o1 = (array.Length > 0 ? array[0] : default);
            o2 = (array.Length > 1 ? array[1] : default);
        }

        public static void Deconstruct<T>(this T[] array, out T o1, out T o2, out T o3)
        {
            o1 = (array.Length > 0 ? array[0] : default);
            o2 = (array.Length > 1 ? array[1] : default);
            o3 = (array.Length > 2 ? array[2] : default);
        }

        public static void Deconstruct<T>(this T[] array, out T o1, out T o2, out T o3, out T o4)
        {
            o1 = (array.Length > 0 ? array[0] : default);
            o2 = (array.Length > 1 ? array[1] : default);
            o3 = (array.Length > 2 ? array[2] : default);
            o4 = (array.Length > 3 ? array[3] : default);
        }
    }
}
