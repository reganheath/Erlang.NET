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
using System.Collections;
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
    public class OtpErlangMap : OtpErlangObject, IEquatable<OtpErlangMap>
    {
        private Dictionary<OtpErlangObject, OtpErlangObject> map = new Dictionary<OtpErlangObject, OtpErlangObject>();

        public OtpErlangMap(OtpErlangObject[] keys, OtpErlangObject[] values)
        {
            if (keys == null)
                throw new ArgumentNullException("keys");
            if (values == null)
                throw new ArgumentNullException("values");
            if (keys.Length != values.Length)
                throw new ArgumentException("keys and values must be the same length");
            keys.Zip(values, (k, v) => { map.Add(k, v); return false; });
        }

        public OtpErlangMap(OtpErlangObject[] keys, int kstart, int kcount, OtpErlangObject[] values, int vstart, int vcount)
        {
            if (keys == null)
                throw new ArgumentNullException("keys");
            if (values == null)
                throw new ArgumentNullException("values");

            OtpErlangObject[] theKeys = keys.Skip(kstart).Take(kcount).ToArray();
            OtpErlangObject[] theValues = values.Skip(vstart).Take(vcount).ToArray();

            if (theKeys.Length != theValues.Length)
                throw new ArgumentException("keys and values must be the same length");

            theKeys.Zip(theValues, (k, v) => (k, v)).Select((o, i) =>
            {
                if (o.k == null)
                    throw new ArgumentException("Map key cannot be null (element" + i + ")");
                if (o.v == null)
                    throw new ArgumentException("Map value cannot be null (element" + i + ")");
                map.Add(o.k, o.v);
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
                OtpErlangObject key = buf.ReadAny();
                OtpErlangObject value = buf.ReadAny();
                map.Add(key, value);
            }
        }

        public int Arity => map.Count;

        public IEnumerable<OtpErlangObject> Keys => map.Keys;

        public IEnumerable<OtpErlangObject> Values => map.Values;

        public OtpErlangObject Put(OtpErlangObject key, OtpErlangObject value)
        {
            if (!map.TryGetValue(key, out OtpErlangObject oldValue))
                oldValue = null;
            map.Add(key, value);
            return oldValue;
        }

        public OtpErlangObject Remove(OtpErlangObject key)
        {
            if (!map.TryGetValue(key, out OtpErlangObject oldValue))
                oldValue = null;
            map.Remove(key);
            return oldValue;
        }

        public OtpErlangObject Get(OtpErlangObject key)
        {
            if (key == null)
                return null;

            if (map.TryGetValue(key, out OtpErlangObject value))
                return value;

            return null;
        }

        public override string ToString() => "#{" + string.Join(",", map.Select((p) => p.Key + " => " + p.Value)) + "}";

        public override void Encode(OtpOutputStream buf)
        {
            buf.WriteMapHead(Arity);
            foreach (var pair in map)
            {
                buf.WriteAny(pair.Key);
                buf.WriteAny(pair.Value);
            }
        }

        public override bool Equals(object o) => Equals(o as OtpErlangMap);

        public bool Equals(OtpErlangMap o)
        {
            if (o == null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            if (Arity != o.Arity)
                return false;
            if (Arity == 0)
                return true;
            //if (GetHashCode() != map.GetHashCode())
            //    return false;
            return map.OrderBy(kvp => kvp.Key).SequenceEqual(o.map.OrderBy(kvp => kvp.Key), new KeyValuePairComparer());
        }

        public override int GetHashCode() => base.GetHashCode();

        protected override int HashCode()
        {
            Hash hash = new Hash(9);
            hash.Combine(Arity);
            foreach (var pair in map)
                hash.Combine(pair.Key.GetHashCode(), pair.Value.GetHashCode());
            return hash.ValueOf();
        }

        public override object Clone()
        {
            OtpErlangMap newMap = (OtpErlangMap)base.Clone();
            newMap.map = new Dictionary<OtpErlangObject, OtpErlangObject>(map);
            return newMap;
        }

        private class KeyValuePairComparer : IEqualityComparer<KeyValuePair<OtpErlangObject, OtpErlangObject>>
        {
            public bool Equals(KeyValuePair<OtpErlangObject, OtpErlangObject> x, KeyValuePair<OtpErlangObject, OtpErlangObject> y)
            {
                return x.Key == y.Key && x.Value == y.Value;
            }

            public int GetHashCode(KeyValuePair<OtpErlangObject, OtpErlangObject> obj)
            {
                Hash hash = new Hash(9);
                hash.Combine(obj.Key.GetHashCode(), obj.Value.GetHashCode());
                return hash.ValueOf();
            }
        }
    }
}

