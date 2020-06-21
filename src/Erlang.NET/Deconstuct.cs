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
