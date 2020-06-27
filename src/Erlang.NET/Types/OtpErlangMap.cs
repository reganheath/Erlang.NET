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
using System;
using System.Collections.Generic;
using System.Linq;

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang maps. Maps are created from one or
     * more arbitrary Erlang terms.
     * 
     * <p>
     * The arity of the map is the number of elements it contains. The keys and
     * values can be retrieved as arrays and the value for a key can be queried.
     * 
     */
    [Serializable]
    public class OtpErlangMap : Dictionary<IOtpErlangObject, IOtpErlangObject>, IOtpErlangObject, IEquatable<OtpErlangMap>, IComparable<OtpErlangMap>
    {
        public int Arity => Count;

        public OtpErlangMap() : base() { }
        public OtpErlangMap(int capacity) : base(capacity) { }
        public OtpErlangMap(IEqualityComparer<IOtpErlangObject> comparer) : base(comparer) { }
        public OtpErlangMap(IDictionary<IOtpErlangObject, IOtpErlangObject> dictionary) : base(dictionary) { }
        public OtpErlangMap(int capacity, IEqualityComparer<IOtpErlangObject> comparer) : base(capacity, comparer) { }
        public OtpErlangMap(IDictionary<IOtpErlangObject, IOtpErlangObject> dictionary, IEqualityComparer<IOtpErlangObject> comparer) : base(dictionary, comparer) { }

        public OtpErlangMap(IEnumerable<IOtpErlangObject> keys, IEnumerable<IOtpErlangObject> values)
        {
            if (keys == null)
                throw new ArgumentNullException(nameof(keys));
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            keys.Zip(values, (k, v) => (k, v)).Select((o, i) =>
            {
                if (o.k == null)
                    throw new ArgumentException($"Map key {i} cannot be null");
                if (o.v == null)
                    throw new ArgumentException($"Map value {i} cannot be null");
                Add(o.k, o.v);
                return false;
            });
        }

        public OtpErlangMap(OtpInputStream buf)
        {
            int arity = buf.ReadMapHead();
            if (arity == 0)
                return;
            for (int i = 0; i < arity; i++)
            {
                IOtpErlangObject key = buf.ReadAny();
                IOtpErlangObject value = buf.ReadAny();
                Add(key, value);
            }
        }

        public override string ToString() => "#{" + string.Join(",", this.Select((p) => p.Key + " => " + p.Value)) + "}";

        public void Encode(OtpOutputStream buf)
        {
            buf.WriteMapHead(Arity);
            foreach (var pair in this)
            {
                buf.WriteAny(pair.Key);
                buf.WriteAny(pair.Value);
            }
        }

        public int CompareTo(object obj) => CompareTo(obj as OtpErlangMap);

        public int CompareTo(OtpErlangMap other)
        {
            if (other is null)
                return 1;
            return OtpErlangObject.CompareTo(this.OrderBy(kvp => kvp.Key), other.OrderBy(kvp => kvp.Key), new KeyValueComparer());
        }

        public override bool Equals(object obj) => Equals(obj as OtpErlangMap);

        public bool Equals(OtpErlangMap o)
        {
            if (o is null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Arity == o.Arity &&
                this.OrderBy(kvp => kvp.Key).SequenceEqual(o.OrderBy(kvp => kvp.Key), new KeyValueEquality());
        }

        public override int GetHashCode() => base.GetHashCode();

        public object Clone() => new OtpErlangMap(this);

        #region Private comparers
        private class KeyValueEquality : IEqualityComparer<KeyValuePair<IOtpErlangObject, IOtpErlangObject>>
        {
            public bool Equals(KeyValuePair<IOtpErlangObject, IOtpErlangObject> x, KeyValuePair<IOtpErlangObject, IOtpErlangObject> y)
            {
                return x.Key == y.Key && x.Value == y.Value;
            }

            public int GetHashCode(KeyValuePair<IOtpErlangObject, IOtpErlangObject> obj)
            {
                int hashCode = 444177069;
                hashCode = hashCode * -1521134295 + obj.Key.GetHashCode();
                hashCode = hashCode * -1521134295 + obj.Value.GetHashCode();
                return hashCode;
            }
        }

        private class KeyValueComparer : IComparer<KeyValuePair<IOtpErlangObject, IOtpErlangObject>>
        {
            public int Compare(KeyValuePair<IOtpErlangObject, IOtpErlangObject> x, KeyValuePair<IOtpErlangObject, IOtpErlangObject> y)
            {
                int res = x.Key.CompareTo(y.Key);
                if (res == 0)
                    res = x.Value.CompareTo(y.Value);
                return res;
            }
        }
        #endregion
    }
}

