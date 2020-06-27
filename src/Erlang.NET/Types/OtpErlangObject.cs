using System;
using System.Collections.Generic;
using System.Linq;

namespace Erlang.NET
{
    public static class OtpErlangObject
    {
        public static bool SequenceEqual<T>(IEnumerable<T> first, IEnumerable<T> second)
        {
            if (ReferenceEquals(first, second))
                return true;
            if (first is null)
                return false;
            if (second is null)
                return false;
            return first.SequenceEqual(second);
        }

        public static int CompareTo<T>(this IEnumerable<T> first, IEnumerable<T> second) where T : IComparable
        {
            if (ReferenceEquals(first, second))
                return 0;
            if (first is null)
                return -1;
            if (second is null)
                return 1;

            using (var fi = first.GetEnumerator())
            using (var si = second.GetEnumerator())
            {
                while (true)
                {
                    if (!fi.MoveNext())
                        return si.MoveNext() ? -1 : 0;
                    if (!si.MoveNext())
                        return 1;
                    int diff = fi.Current.CompareTo(si.Current);
                    if (diff != 0)
                        return diff;
                }
            }
        }

        public static int CompareTo<T>(this IEnumerable<T> first, IEnumerable<T> second, IComparer<T> comparer)
        {
            if (ReferenceEquals(first, second))
                return 0;
            if (first is null)
                return -1;
            if (second is null)
                return 1;

            using (var fi = first.GetEnumerator())
            using (var si = second.GetEnumerator())
            {
                while (true)
                {
                    if (!fi.MoveNext())
                        return si.MoveNext() ? -1 : 0;
                    if (!si.MoveNext())
                        return 1;
                    int diff = comparer.Compare(fi.Current, si.Current);
                    if (diff != 0)
                        return diff;
                }
            }
        }
    }
}
