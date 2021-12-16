using System;
using System.Collections;
using System.Collections.Generic;

namespace YANCL
{
    public class Table2
    {

        const int initialSize = 4;
        const int multiplier = 2;

        private LuaValue[]? array;
        private TableEntry[]? map;
        private int arrayCount;
        private int mapCount;

        public struct MapEnumerator : IEnumerator<KeyValuePair<LuaValue, LuaValue>>
        {
            public Table2 Table;
            public int Index;

            public KeyValuePair<LuaValue, LuaValue> Current {
                get {
                    if (Index < Table.arrayCount) {
                        return new KeyValuePair<LuaValue, LuaValue>(Index + 1, Table.array![Index]);
                    }
                    var idx = Index - Table.arrayCount;
                    if (idx < Table.mapCount) {
                        return new KeyValuePair<LuaValue, LuaValue>(Table.map![idx].key, Table.map[idx].value);
                    }
                    throw new InvalidOperationException();
                }
            }

            object IEnumerator.Current => Current;

            public void Dispose(){}

            public bool MoveNext()
            {
                Index++;
                return Index < Table.arrayCount + Table.mapCount;
            }

            public void Reset() => Index = -1;
        }
        
        private int Lookup(LuaValue key, out int prev, out bool lt) {
            var hash = key.GetHashCode();
            prev = 0;
            lt = false;
            int pos = 0;
            while (pos >= 0) {
                if (map![pos].key == key) {
                    return pos;
                }
                prev = pos;
                lt = hash < map[pos].hash;
                if (lt) {
                    pos = map[pos].lt - 1;
                } else {
                    pos = map[pos].ge - 1;
                }
            }
            return -1;
        }

        private static int Expand<T>(ref T[] array, ref int count) {
            if (count + 1 > array.Length) {
                Array.Resize(ref array, array.Length * multiplier);
            }
            return count++;
        }

        private void MapSet(LuaValue key, LuaValue value) {
            if (map == null) {
                map = new TableEntry[initialSize];
            }
            var pos = Lookup(key, out int prev, out bool lt);
            if (pos > 0) {
                map[pos].value = value;
            } else {
                var newSlot = Expand(ref map, ref mapCount);
                if (lt) {
                    map[prev].lt = newSlot + 1;
                } else {
                    map[prev].ge = newSlot + 1;
                }
                map[newSlot] = new TableEntry {
                    hash = key.GetHashCode(),
                    key = key,
                    value = value
                };
            }
        }
    }

    internal struct TableEntry
    {
        public int hash;
        public LuaValue key;
        public LuaValue value;
        public int lt;
        public int ge;
    }
}