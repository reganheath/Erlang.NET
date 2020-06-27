/*
 * Copyright 2020 Regan Heath
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Collections.Generic;

namespace Erlang.NET
{
    public static class Deconstuct
    {
        public static void Deconstruct<T>(this IEnumerable<T> obj, out T o1)
        {
            using (var i = obj.GetEnumerator())
                o1 = i.MoveNext() ? (T)i.Current : default;
        }

        public static void Deconstruct<T>(this IEnumerable<T> obj, out T o1, out T o2)
        {
            using (var i = obj.GetEnumerator())
            {
                o1 = i.MoveNext() ? (T)i.Current : default;
                o2 = i.MoveNext() ? (T)i.Current : default;
            }
        }

        public static void Deconstruct<T>(this IEnumerable<T> obj, out T o1, out T o2, out T o3)
        {
            using (var i = obj.GetEnumerator())
            {
                o1 = i.MoveNext() ? (T)i.Current : default;
                o2 = i.MoveNext() ? (T)i.Current : default;
                o3 = i.MoveNext() ? (T)i.Current : default;
            }
        }

        public static void Deconstruct<T>(this IEnumerable<T> obj, out T o1, out T o2, out T o3, out T o4)
        {
            using (var i = obj.GetEnumerator())
            {
                o1 = i.MoveNext() ? (T)i.Current : default;
                o2 = i.MoveNext() ? (T)i.Current : default;
                o3 = i.MoveNext() ? (T)i.Current : default;
                o4 = i.MoveNext() ? (T)i.Current : default;
            }
        }

        public static void Deconstruct<T>(this IEnumerable<T> obj, out T o1, out T o2, out T o3, out T o4, out T o5)
        {
            using (var i = obj.GetEnumerator())
            {
                o1 = i.MoveNext() ? (T)i.Current : default;
                o2 = i.MoveNext() ? (T)i.Current : default;
                o3 = i.MoveNext() ? (T)i.Current : default;
                o4 = i.MoveNext() ? (T)i.Current : default;
                o5 = i.MoveNext() ? (T)i.Current : default;
            }
        }

        public static void Deconstruct<T>(this IEnumerable<T> obj, out T o1, out T o2, out T o3, out T o4, out T o5, out T o6)
        {
            using (var i = obj.GetEnumerator())
            {
                o1 = i.MoveNext() ? (T)i.Current : default;
                o2 = i.MoveNext() ? (T)i.Current : default;
                o3 = i.MoveNext() ? (T)i.Current : default;
                o4 = i.MoveNext() ? (T)i.Current : default;
                o5 = i.MoveNext() ? (T)i.Current : default;
                o6 = i.MoveNext() ? (T)i.Current : default;
            }
        }

        public static void Deconstruct<T>(this IEnumerable<T> obj, out T o1, out T o2, out T o3, out T o4, out T o5, out T o6, out T o7)
        {
            using (var i = obj.GetEnumerator())
            {
                o1 = i.MoveNext() ? (T)i.Current : default;
                o2 = i.MoveNext() ? (T)i.Current : default;
                o3 = i.MoveNext() ? (T)i.Current : default;
                o4 = i.MoveNext() ? (T)i.Current : default;
                o5 = i.MoveNext() ? (T)i.Current : default;
                o6 = i.MoveNext() ? (T)i.Current : default;
                o7 = i.MoveNext() ? (T)i.Current : default;
            }
        }

        public static void Deconstruct<T>(this IEnumerable<T> obj, out T o1, out T o2, out T o3, out T o4, out T o5, out T o6, out T o7, out T o8)
        {
            using (var i = obj.GetEnumerator())
            {
                o1 = i.MoveNext() ? (T)i.Current : default;
                o2 = i.MoveNext() ? (T)i.Current : default;
                o3 = i.MoveNext() ? (T)i.Current : default;
                o4 = i.MoveNext() ? (T)i.Current : default;
                o5 = i.MoveNext() ? (T)i.Current : default;
                o6 = i.MoveNext() ? (T)i.Current : default;
                o7 = i.MoveNext() ? (T)i.Current : default;
                o8 = i.MoveNext() ? (T)i.Current : default;
            }
        }

        public static void Deconstruct<T>(this IEnumerable<T> obj, out T o1, out T o2, out T o3, out T o4, out T o5, out T o6, out T o7, out T o8, out T o9)
        {
            using (var i = obj.GetEnumerator())
            {
                o1 = i.MoveNext() ? (T)i.Current : default;
                o2 = i.MoveNext() ? (T)i.Current : default;
                o3 = i.MoveNext() ? (T)i.Current : default;
                o4 = i.MoveNext() ? (T)i.Current : default;
                o5 = i.MoveNext() ? (T)i.Current : default;
                o6 = i.MoveNext() ? (T)i.Current : default;
                o7 = i.MoveNext() ? (T)i.Current : default;
                o8 = i.MoveNext() ? (T)i.Current : default;
                o9 = i.MoveNext() ? (T)i.Current : default;
            }
        }

        public static void Deconstruct<T>(this IEnumerable<T> obj, out T o1, out T o2, out T o3, out T o4, out T o5, out T o6, out T o7, out T o8, out T o9, out T o10)
        {
            using (var i = obj.GetEnumerator())
            {
                o1 = i.MoveNext() ? (T)i.Current : default;
                o2 = i.MoveNext() ? (T)i.Current : default;
                o3 = i.MoveNext() ? (T)i.Current : default;
                o4 = i.MoveNext() ? (T)i.Current : default;
                o5 = i.MoveNext() ? (T)i.Current : default;
                o6 = i.MoveNext() ? (T)i.Current : default;
                o7 = i.MoveNext() ? (T)i.Current : default;
                o8 = i.MoveNext() ? (T)i.Current : default;
                o9 = i.MoveNext() ? (T)i.Current : default;
                o10 = i.MoveNext() ? (T)i.Current : default;
            }
        }
    }
}
