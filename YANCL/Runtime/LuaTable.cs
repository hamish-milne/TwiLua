using System;
using System.Collections;
using System.Collections.Generic;

namespace YANCL
{

    public sealed class LuaTable : IEnumerable<(LuaValue key, LuaValue value)>
    {
        private LuaMap? map = null;
        private List<LuaValue>? array = null;

        public LuaMap Map => map ??= new LuaMap();
        public List<LuaValue> Array => array ??= new List<LuaValue>();
        public LuaTable? MetaTable { get; set; }
        public int Length => array == null ? 0 : array.Count;

        public LuaTable() { }
        public LuaTable(int initialCapacity) {
            array = new List<LuaValue>(initialCapacity);
        }

        private static bool IsArrayIndex(in LuaValue key, out int idx) {
            if (key.Type == LuaType.NUMBER && key.Number > 0 && key.Number % 1 == 0) {
                idx = (int)key.Number - 1;
                return true;
            }
            idx = -1;
            return false;
        }

        public Enumerator GetEnumerator() => new Enumerator(this);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        IEnumerator<(LuaValue key, LuaValue value)> IEnumerable<(LuaValue key, LuaValue value)>.GetEnumerator() => GetEnumerator();

        private void MoveContiguousIntegerKeys() {
            if (array != null && map != null) {
                LuaValue? mapVal;
                while ((mapVal = map.Delete(array.Count + 1)) != null) {
                    array.Add(mapVal.Value);
                }
            }
        }

        public LuaValue this[in LuaValue key] {
            get {
                if (array != null && IsArrayIndex(key, out var idx) && idx < array.Count) {
                    return array[idx];
                }
                return map == null ? LuaValue.Nil : map[key];
            }
            set {
                if (IsArrayIndex(key, out var idx)) {
                    if (array == null) {
                        if (idx == 0) {
                            array = new List<LuaValue> { value };
                        } else {
                            Map[key] = value;
                        }
                    } else if (idx < array.Count) {
                        array[idx] = value;
                    } else if (idx == array.Count) {
                        array.Add(value);
                        MoveContiguousIntegerKeys();
                    } else {
                        Map[key] = value;
                    }
                } else {
                    Map[key] = value;
                }
            }
        }

        public void Add(in LuaValue value) {
            Array.Add(value);
            MoveContiguousIntegerKeys();
        }

        public void Insert(int position, in LuaValue value) {
            Array.Insert(position, value);
            MoveContiguousIntegerKeys();
        }

        public (LuaValue key, LuaValue value)? Next(in LuaValue key) {
            if (key == LuaValue.Nil) {
                if (array != null) {
                    return (1, array[0]);
                }
                if (map != null) {
                    return map.First();
                }
            } else if (array != null && IsArrayIndex(key, out var idx)) {
                if (idx < (array.Count - 1)) {
                    return (idx + 2, array[idx + 1]);
                }
                if (map != null && idx == (array.Count - 1)) {
                    return map.First();
                }
            } else if (map != null) {
                return map.Next(key);
            }
            return null;
        }

        public void Add(in LuaValue key, in LuaValue value) => this[key] = value;
        public void Add(in LuaValue key, LuaCFunction value) => this[key] = value;

        public struct Enumerator : IEnumerator<(LuaValue key, LuaValue value)>
        {
            private readonly LuaTable table;
            private int arrayIdx;
            private LuaMap.Enumerator mapEnumerator;

            public Enumerator(LuaTable table) {
                this.table = table;
                arrayIdx = 0;
                mapEnumerator = table.Map.GetEnumerator();
            }

            public (LuaValue key, LuaValue value) Current {
                get {
                    if (arrayIdx < table.Length) {
                        return (arrayIdx + 1, table.Array[arrayIdx]);
                    }
                    var pair = mapEnumerator.Current;
                    return (pair.Key, pair.Value);
                }
            }

            object IEnumerator.Current => Current;

            void IDisposable.Dispose() { }

            public bool MoveNext()
            {
                if (arrayIdx < (table.Length - 1)) {
                    arrayIdx++;
                    return true;
                }
                return mapEnumerator.MoveNext();
            }

            public void Reset()
            {
                arrayIdx = -1;
                mapEnumerator.Reset();
            }
        }


    }
}