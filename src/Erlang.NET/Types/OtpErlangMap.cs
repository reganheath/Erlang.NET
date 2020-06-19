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
    public class OtpErlangMap : OtpErlangObject, IDictionary<OtpErlangObject, OtpErlangObject>
    {
        private Dictionary<OtpErlangObject, OtpErlangObject> map = new Dictionary<OtpErlangObject, OtpErlangObject>();

        public int arity() => map.Count;
        public OtpErlangObject[] keys() => map.Keys.ToArray();
        public OtpErlangObject[] values() => map.Values.ToArray();

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
            if (arity > 0)
            {
                for (int i = 0; i < arity; i++)
                {
                    OtpErlangObject key = buf.ReadAny();
                    OtpErlangObject value = buf.ReadAny();
                    map.Add(key, value);
                }
            }
        }

        public OtpErlangObject put(OtpErlangObject key, OtpErlangObject value)
        {
            OtpErlangObject oldValue;
            if (!map.TryGetValue(key, out oldValue))
                oldValue = null;
            map.Add(key, value);
            return oldValue;
        }

        public OtpErlangObject remove(OtpErlangObject key)
        {
            OtpErlangObject oldValue;
            if (!map.TryGetValue(key, out oldValue))
                oldValue = null;
            map.Remove(key);
            return oldValue;
        }

        public OtpErlangObject get(OtpErlangObject key)
        {
            if (key == null)
                return null;

            OtpErlangObject value;
            if (map.TryGetValue(key, out value))
                return value;

            return null;
        }

        public override string ToString()
        {
            return "#{" + string.Join(",", map.Select((p) => p.Key + " => " + p.Value)) + "}";
        }

        public override void Encode(OtpOutputStream buf)
        {
            buf.WriteMapHead(arity());
            foreach (var p in map)
            {
                buf.WriteAny(p.Key);
                buf.WriteAny(p.Value);
            }
        }

        public override bool Equals(object o) => Equals(o as OtpErlangMap);

        public bool Equals(OtpErlangMap o)
        {
            if (o == null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            if (arity() != o.arity())
                return false;
            if (arity() == 0)
                return true;
            //if (GetHashCode() != map.GetHashCode())
            //    return false;
            return o.OrderBy(kvp => kvp.Key).SequenceEqual(o.map.OrderBy(kvp => kvp.Key));
        }

        public override int GetHashCode() => base.GetHashCode();

        protected override int DoHashCode()
        {
            OtpErlangObject.Hash hash = new OtpErlangObject.Hash(9);
            hash.Combine(arity());
            foreach (var key in map.Keys)
                hash.Combine(key.GetHashCode());
            foreach (var value in map.Values)
                hash.Combine(value.GetHashCode());
            return hash.ValueOf();
        }

        public override object Clone()
        {
            OtpErlangMap newMap = (OtpErlangMap)base.Clone();
            newMap.map = new Dictionary<OtpErlangObject, OtpErlangObject>(map);
            return newMap;
        }

        #region IDictionary
        public ICollection<OtpErlangObject> Keys => map.Keys;
        public ICollection<OtpErlangObject> Values => map.Values;
        public int Count => map.Count;
        public bool IsReadOnly => false;
        public OtpErlangObject this[OtpErlangObject key] { get => map[key]; set => map[key] = value; }
        public bool ContainsKey(OtpErlangObject key) => map.ContainsKey(key);
        public void Add(OtpErlangObject key, OtpErlangObject value) => map.Add(key, value);
        public bool Remove(OtpErlangObject key) => map.Remove(key);
        public bool TryGetValue(OtpErlangObject key, out OtpErlangObject value) => map.TryGetValue(key, out value);
        public void Add(KeyValuePair<OtpErlangObject, OtpErlangObject> item) => map.Add(item.Key, item.Value);
        public void Clear() => map.Clear();
        public bool Contains(KeyValuePair<OtpErlangObject, OtpErlangObject> item) => map.Contains(item);
        public void CopyTo(KeyValuePair<OtpErlangObject, OtpErlangObject>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<OtpErlangObject, OtpErlangObject>>)map).CopyTo(array, arrayIndex);
        }
        public bool Remove(KeyValuePair<OtpErlangObject, OtpErlangObject> item)
        {
            if (map.TryGetValue(item.Key, out OtpErlangObject hasValue) && object.Equals(hasValue, item.Value))
                return map.Remove(item.Key);
            return false;
        }
        public IEnumerator<KeyValuePair<OtpErlangObject, OtpErlangObject>> GetEnumerator() => map.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => map.GetEnumerator();
        #endregion
    }
}

