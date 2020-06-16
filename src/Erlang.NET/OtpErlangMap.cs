using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

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
        // don't change this!
        internal static readonly new long serialVersionUID = -6410770117696198497L;

        private Dictionary<OtpErlangObject, OtpErlangObject> dict = new Dictionary<OtpErlangObject, OtpErlangObject>();

        public int arity() => dict.Count;

        public OtpErlangObject[] keys() => dict.Keys.ToArray();

        public OtpErlangObject[] values() => dict.Values.ToArray();


        public OtpErlangMap(OtpErlangObject[] keys, OtpErlangObject[] values)
        {
            if (keys.Length != values.Length)
                throw new ArgumentException("keys and values must be the same length");
            keys.Zip(values, (k, v) => { dict.Add(k, v); return false; });
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
                dict.Add(o.k, o.v);
                return false;
            });
        }

        public OtpErlangMap(OtpInputStream buf)
        {
            int arity = buf.read_map_head();
            if (arity > 0)
            {
                for(int i = 0; i < arity; i++)
                {
                    OtpErlangObject key = buf.read_any();
                    OtpErlangObject value = buf.read_any();
                    dict.Add(key, value);
                }
            }
        }

        public OtpErlangObject get(OtpErlangObject key)
        {
            if (key == null)
                return null;

            OtpErlangObject value;
            if (dict.TryGetValue(key, out value))
                return value;

            return null;
        }

        public override string ToString()
        {
            return "#{" + String.Join(",", dict.Select((p) => p.Key + " => " + p.Value)) + "}";
        }

        public override void encode(OtpOutputStream buf)
        {
            buf.write_map_head(arity());
            foreach(var p in dict)
            {
                buf.write_any(p.Key);
                buf.write_any(p.Value);
            }
        }

        public override bool Equals(object o)
        {
            if (o == null)
                return false;
            if (o.GetType() != typeof(OtpErlangMap))
                return false;
            return Equals((OtpErlangMap)o);
        }

        public bool Equals(OtpErlangMap map)
        {
            if (map == null)
                return false;
            if (ReferenceEquals(this, map))
                return true;
            //if (GetHashCode() != map.GetHashCode())
            //    return false;
            return dict.OrderBy(kvp => kvp.Key).SequenceEqual(map.dict.OrderBy(kvp => kvp.Key));
        }

        public override int GetHashCode() => base.GetHashCode();

        protected override int doHashCode()
        {
            OtpErlangObject.Hash hash = new OtpErlangObject.Hash(9);
            hash.combine(arity());
            foreach (var key in dict.Keys)
                hash.combine(key.GetHashCode());
            foreach (var value in dict.Values)
                hash.combine(value.GetHashCode());
            return hash.valueOf();
        }

        public override object Clone()
        {
            OtpErlangMap newMap = (OtpErlangMap)base.Clone();
            newMap.dict = new Dictionary<OtpErlangObject, OtpErlangObject>(this.dict);
            return newMap;
        }

        #region IDictionary
        public ICollection<OtpErlangObject> Keys => dict.Keys;
        public ICollection<OtpErlangObject> Values => dict.Values;
        public int Count => dict.Count;
        public bool IsReadOnly => false;
        public OtpErlangObject this[OtpErlangObject key] { get => dict[key]; set => dict[key] = value; }
        public bool ContainsKey(OtpErlangObject key) => dict.ContainsKey(key);
        public void Add(OtpErlangObject key, OtpErlangObject value) => dict.Add(key, value);
        public bool Remove(OtpErlangObject key) => dict.Remove(key);
        public bool TryGetValue(OtpErlangObject key, out OtpErlangObject value) => dict.TryGetValue(key, out value);
        public void Add(KeyValuePair<OtpErlangObject, OtpErlangObject> item) => dict.Add(item.Key, item.Value);
        public void Clear() => dict.Clear();
        public bool Contains(KeyValuePair<OtpErlangObject, OtpErlangObject> item) => dict.Contains(item);
        public void CopyTo(KeyValuePair<OtpErlangObject, OtpErlangObject>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<OtpErlangObject, OtpErlangObject>>)dict).CopyTo(array, arrayIndex);
        }
        public bool Remove(KeyValuePair<OtpErlangObject, OtpErlangObject> item)
        {
            if (dict.TryGetValue(item.Key, out OtpErlangObject hasValue) && object.Equals(hasValue, item.Value))
                return dict.Remove(item.Key);
            return false;
        }
        public IEnumerator<KeyValuePair<OtpErlangObject, OtpErlangObject>> GetEnumerator() => dict.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => dict.GetEnumerator();
        #endregion
    }
}

