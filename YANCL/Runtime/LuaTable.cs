using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YANCL
{

    public sealed class LuaTable : ICollection<(LuaValue key, LuaValue value)>
    {
#region Details
        const int initialSize = 4;
        const int multiplier = 2;

        struct MapEntry
        {
            public LuaValue key;
            public LuaValue value;
        }

        private LuaValue[]? array;
        private MapEntry[]? map;
        private int arrayCount;
        private int mapCount;

        public struct MapEnumerator : IEnumerator<(LuaValue key, LuaValue value)>
        {
            public LuaTable Table;
            public int Index;

            public (LuaValue key, LuaValue value) Current => Table.Iterate(Index) ?? throw new InvalidOperationException();

            object IEnumerator.Current => Current;

            public void Dispose(){}

            public bool MoveNext()
            {
                Index++;
                return Index < Table.arrayCount + Table.mapCount;
            }

            public void Reset() => Index = -1;
        }

        IEnumerator<(LuaValue key, LuaValue value)> IEnumerable<(LuaValue key, LuaValue value)>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private class HashComparer : IComparer<MapEntry>
        {
            public int Compare(MapEntry x, MapEntry y) => x.key.Hash.CompareTo(y.key.Hash);
        }
        private static readonly HashComparer hashComparer = new HashComparer();
        
        private bool Lookup(LuaValue key, out int idx) {
            if (map == null) {
                idx = 0;
                return false;
            }
            idx = Array.BinarySearch(map!, 0, mapCount, new MapEntry { key = key }, hashComparer);
            while (idx > 0 && map![idx - 1].key.Hash == key.Hash) {
                idx--;
            }
            if (idx >= 0) {
                do {
                    if (map![idx].key.Equals(key)) {
                        return true;
                    }
                } while (++idx < mapCount && map![idx].key.Hash == key.Hash);
                return false;
            }
            idx = ~idx;
            return false;
        }

        private void EnsureCapacity<T>(ref T[]? arr, int capacity) {
            var currentSize = arr?.Length ?? initialSize;
            while (currentSize < capacity) {
                currentSize *= multiplier;
            }
            Array.Resize(ref arr, currentSize);
        }

        private void MapSet(LuaValue key, LuaValue value) {
            if (Lookup(key, out var idx)) {
                map![idx].value = value;
                return;
            }
            mapCount++;
            EnsureCapacity(ref map, mapCount);
            Array.Copy(map!, idx, map!, idx + 1, mapCount - idx - 1);
            map![idx] = new MapEntry { key = key, value = value };
        }

        private LuaValue MapGet(LuaValue key) {
            if (Lookup(key, out var idx)) {
                return map![idx].value;
            }
            return LuaValue.Nil;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetArrayIndex(in LuaValue key, out int idx) {
            if (key.Type == LuaType.NUMBER && key.Number == (int)key.Number && key.Number >= 1) {
                idx = (int)key.Number - 1;
                return true;
            }
            idx = 0;
            return false;
        }
#endregion Details

        public LuaValue this[in LuaValue key] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (TryGetArrayIndex(key, out var idx)) {
                    if (idx < arrayCount) {
                        return array![idx];
                    } else {
                        return LuaValue.Nil;
                    }
                } else {
                    return MapGet(key);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                if (TryGetArrayIndex(key, out var idx)) {
                    if (idx >= arrayCount) {
                        arrayCount = idx + 1;
                        EnsureCapacity(ref array, arrayCount);
                    }
                    array![idx] = value;
                } else {
                    MapSet(key, value);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in LuaValue key, in LuaValue value) {
            this[key] = value;
        }

        // This prevents the need for explicit casts for standard library definitions
        public void Add(in LuaValue key, LuaCFunction value) {
            this[key] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in LuaValue value) {
            arrayCount++;
            EnsureCapacity(ref array, arrayCount);
            this[arrayCount] = value;
        }

        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => arrayCount;
        }

        public bool IsReadOnly => false;

        public void Insert(int pos, in LuaValue value) {
            if (pos < 0) {
                throw new ArgumentOutOfRangeException(nameof(pos));
            }
            arrayCount++;
            EnsureCapacity(ref array, arrayCount);
            Array.Copy(array!, pos, array!, pos + 1, arrayCount - pos - 1);
            array![pos] = value;
        }

        public LuaValue RemoveAt(int pos) {
            if (pos >= arrayCount) {
                return LuaValue.Nil;
            }
            var ret = array![pos];
            if (pos < arrayCount - 1) {
                Array.Copy(array!, pos + 1, array!, pos, arrayCount - pos - 1);
            }
            array[arrayCount - 1] = LuaValue.Nil;
            arrayCount--;
            return ret;
        }

        public MapEnumerator GetEnumerator() => new MapEnumerator { Table = this, Index = -1 };

        public (LuaValue key, LuaValue value)? Iterate(int i) {
            if (i < arrayCount) {
                return (i + 1, array![i]);
            }
            var idx = i - arrayCount;
            if (idx < mapCount) {
                return (map![idx].key, map[idx].value);
            }
            return null;
        }

        public (LuaValue key, LuaValue value)? Start() => Iterate(0);

        public (LuaValue key, LuaValue value)? Next(in LuaValue key) {
            if (TryGetArrayIndex(key, out var idx) && idx < arrayCount) {
                idx++;
                return (idx, array![idx]);
            }
            if (Lookup(key, out idx) && idx < mapCount) {
                var mapItem = map![idx + 1];
                return (mapItem.key, mapItem.value);
            }
            return null;
        }

        void ICollection<(LuaValue key, LuaValue value)>.Add((LuaValue key, LuaValue value) item) => Add(item.key, item.value);

        public void Clear()
        {
            arrayCount = 0;
            mapCount = 0;
            array = null;
            map = null;
        }

        bool ICollection<(LuaValue key, LuaValue value)>.Contains((LuaValue key, LuaValue value) item) => this[item.key].Equals(item.value);

        void ICollection<(LuaValue key, LuaValue value)>.CopyTo((LuaValue key, LuaValue value)[] array, int arrayIndex)
        {
            foreach (var (key, value) in this) {
                array[arrayIndex++] = (key, value);
            }
        }

        bool ICollection<(LuaValue key, LuaValue value)>.Remove((LuaValue key, LuaValue value) item)
        {
            if (this[item.key].Equals(item.value)) {
                this[item.key] = LuaValue.Nil;
                return true;
            }
            return false;
        }
    }
}