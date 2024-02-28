using System;
using System.Collections;
using System.Collections.Generic;
using Pair = System.Collections.Generic.KeyValuePair<TwiLua.LuaValue, TwiLua.LuaValue>;

namespace TwiLua
{
    /// <summary>
    /// The map component of a Lua table.
    /// </summary>
    /// <remarks>
    /// This class is a custom dictionary implementation that allows for stateless iteration.
    /// It does not implement Lua semantics regarding nil keys and values.
    /// </remarks>
    public sealed class LuaMap : IDictionary<LuaValue, LuaValue>
    {
        public KeyCollection Keys => new KeyCollection(this);
        public ValueCollection Values => new ValueCollection(this);
        ICollection<LuaValue> IDictionary<LuaValue, LuaValue>.Keys => Keys;
        ICollection<LuaValue> IDictionary<LuaValue, LuaValue>.Values => Values;

        public int Count => _Count(Root);

        bool ICollection<Pair>.IsReadOnly => false;

        public LuaValue this[LuaValue key] {
            get => (Find(key) ?? throw new KeyNotFoundException()).Value;
            set => Insert(key, value, allowOverwrite: true);
        }

        public void Add(LuaValue key, LuaValue value) => Insert(key, value, allowOverwrite: false);

        public bool ContainsKey(LuaValue key) => Find(key) != null;

        public bool Remove(LuaValue key) => Delete(key).HasValue;

        public bool TryGetValue(in LuaValue key, out LuaValue value)
        {
            var node = Find(key);
            if (node != null) {
                value = node.Value;
                return true;
            }
            value = LuaValue.Nil;
            return false;
        }

        bool IDictionary<LuaValue, LuaValue>.TryGetValue(LuaValue key, out LuaValue value) => TryGetValue(key, out value);

        void ICollection<Pair>.Add(Pair item) => Insert(item.Key, item.Value, allowOverwrite: false);

        public void Clear() => Root = null;

        public bool Contains(Pair item) => Find(item.Key)?.Value == item.Value;

        public void CopyTo(Pair[] array, int arrayIndex)
        {
            foreach (var pair in this) {
                array[arrayIndex++] = pair;
            }
        }

        public bool Remove(Pair item)
        {
            if (Find(item.Key)?.Value != item.Value) {
                return false;
            }
            Delete(item.Key);
            return true;
        }


        #region LLRB tree implementation
        class Node {
            public Node? Left;
            public Node? Right;
            public LuaValue Key;
            public LuaValue Value;
            public bool IsRed;
        }

        private Node? Root;

        private Node? Find(in LuaValue key) {
            var node = Root;
            while (node != null) {
                if (key.Equals(node.Key)) {
                    return node;
                } else if (key.Hash < node.Key.Hash) {
                    node = node.Left;
                } else {
                    node = node.Right;
                }
            }
            return null;
        }

        public (LuaValue key, LuaValue value)? Next(in LuaValue key) {
            var node = Root;
            Node? found = null;
            while (node != null) {
                if (node.Key.Hash > key.Hash) {
                    found = node;
                    node = node.Left;
                } else if (node.Key.Equals(key)) {
                    if (node.Right != null) {
                        found = node.Right;
                    }
                    break;
                } else {
                    node = node.Right;
                }
            }
            if (found == null) {
                return null;
            }
            return (found.Key, found.Value);
        }

        public (LuaValue key, LuaValue value)? First() {
            var node = Root;
            if (node == null) {
                return null;
            }
            while (node.Left != null) {
                node = node.Left;
            }
            return (node.Key, node.Value);
        }
        
        private static void FlipColors(Node node) {
            node.IsRed = !node.IsRed;
            node.Left!.IsRed = !node.Left.IsRed;
            node.Right!.IsRed = !node.Right.IsRed;
        }

        private static bool IsRed(Node? node) => node != null && node.IsRed;

        private static Node RotateLeft(Node h) {
            var x = h.Right ?? throw new InvalidOperationException();
            h.Right = x.Left;
            x.Left = h;
            x.IsRed = h.IsRed;
            h.IsRed = true;
            return x;
        }

        private static Node RotateRight(Node h) {
            var x = h.Left ?? throw new InvalidOperationException();
            h.Left = x.Right;
            x.Right = h;
            x.IsRed = h.IsRed;
            h.IsRed = true;
            return x;
        }

        private void Insert(in LuaValue key, in LuaValue value, bool allowOverwrite) {
            Root = Insert(Root, key, value, allowOverwrite);
            Root.IsRed = false;
        }

        private static Node Insert(Node? h, in LuaValue key, in LuaValue value, bool allowOverwrite) {
            if (h == null) {
                return new Node {
                    Key = key,
                    Value = value,
                    IsRed = true
                };
            }
            if (key.Equals(h.Key)) {
                if (!allowOverwrite) {
                    throw new ArgumentException("Key already exists");
                }
                h.Value = value;
            } else if (key.Hash < h.Key.Hash) {
                h.Left = Insert(h.Left, key, value, allowOverwrite);
            } else {
                h.Right = Insert(h.Right, key, value, allowOverwrite);
            }
            return FixUp(h);
        }

        private static Node MoveRedLeft(Node h) {
            FlipColors(h);
            if (IsRed(h.Right!.Left)) {
                h.Right = RotateRight(h.Right);
                h = RotateLeft(h);
                FlipColors(h);
            }
            return h;
        }

        private static Node MoveRedRight(Node h) {
            FlipColors(h);
            if (IsRed(h.Left!.Left)) {
                h = RotateRight(h);
                FlipColors(h);
            }
            return h;
        }

        public LuaValue? Delete(in LuaValue key) {
            Root = Delete(Root, key, out var value);
            if (Root != null) {
                Root.IsRed = false;
            }
            return value;
        }

        private static Node? DeleteMin(Node h) {
            if (h.Left == null) {
                return null;
            }
            if (!IsRed(h.Left) && !IsRed(h.Left.Left)) {
                h = MoveRedLeft(h);
            }
            h.Left = DeleteMin(h.Left!);
            return FixUp(h);
        }

        private static Node? Delete(Node? h, in LuaValue key, out LuaValue? value) {
            value = null;
            if (h == null) {
                return null;
            }
            if (key.Hash < h.Key.Hash) {
                if (h.Left == null) {
                    return h;
                }
                if (!IsRed(h.Left) && !IsRed(h.Left!.Left)) {
                    h = MoveRedLeft(h);
                }
                h.Left = Delete(h.Left, key, out value);
            } else {
                if (IsRed(h.Left)) {
                    h = RotateRight(h);
                }
                if (key.Equals(h.Key) && !IsRed(h.Right)) {
                    value = h.Value;
                    return null;
                }
                if (h.Right == null) {
                    return h;
                }
                if (!IsRed(h.Right) && !IsRed(h.Right!.Left)) {
                    h = MoveRedRight(h);
                }
                if (key.Equals(h.Key)) {
                    value = h.Value;
                    var x = h.Right!;
                    while (x.Left != null) {
                        x = x.Left;
                    }
                    h.Key = x.Key;
                    h.Value = x.Value;
                    h.Right = DeleteMin(h.Right!);
                } else {
                    h.Right = Delete(h.Right, key, out value);
                }
            }
            return FixUp(h);
        }

        private static Node FixUp(Node h) {
            if (IsRed(h.Right) && !IsRed(h.Left)) {
                h = RotateLeft(h);
            }
            if (IsRed(h.Left) && IsRed(h.Left!.Left)) {
                h = RotateRight(h);
            }
            if (IsRed(h.Left) && IsRed(h.Right)) {
                FlipColors(h);
            }
            return h;
        }

        private Node? GetParent(Node node) {
            var parent = Root;
            while (parent != null) {
                if (parent.Left == node || parent.Right == node) {
                    return parent;
                }
                if (node.Key.Hash < parent.Key.Hash) {
                    parent = parent.Left;
                } else {
                    parent = parent.Right;
                }
            }
            return null;
        }

        private static int _Count(Node? node) {
            if (node == null) {
                return 0;
            }
            return _Count(node.Left) + _Count(node.Right) + 1;
        }

        #endregion

        #region Enumeration

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        IEnumerator<Pair> IEnumerable<Pair>.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<Pair>
        {
            private LuaMap obj;
            private Node? node;

            public Enumerator(LuaMap obj) {
                this.obj = obj;
                this.node = null;
            }

            public Pair Current {
                get {
                    if (node == null) {
                        throw new InvalidOperationException();
                    }
                    return new Pair(node.Key, node.Value);
                }
            }

            object IEnumerator.Current => Current;

            void IDisposable.Dispose()
            {
            }

            public bool MoveNext()
            {
                if (node == null) {
                    node = obj.Root;
                    if (node == null) {
                        return false;
                    }
                    while (node.Left != null) {
                        node = node.Left;
                    }
                    return true;
                }
                var parent = obj.GetParent(node);
                if (parent == null) {
                    return false;
                }
                var sibling = parent.Right;
                if (node == sibling || sibling == null) {
                    node = parent;
                    return true;
                }
                while (sibling.Left != null) {
                    sibling = sibling.Left;
                }
                node = sibling;
                return true;
            }

            public void Reset() => node = null;
        }

        #endregion

        #region Key collection

        public struct KeyCollection : ICollection<LuaValue>
        {
            private LuaMap obj;
            public KeyCollection(LuaMap obj) {
                this.obj = obj;
            }
            public int Count => obj.Count;
            public bool IsReadOnly => true;
            public void Add(LuaValue item) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public bool Remove(LuaValue item) => throw new NotSupportedException();
            public bool Contains(LuaValue item) => obj.ContainsKey(item);
            public KeyEnumerator GetEnumerator() => new KeyEnumerator(obj);
            IEnumerator<LuaValue> IEnumerable<LuaValue>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public void CopyTo(LuaValue[] array, int arrayIndex)
            {
                foreach (var key in this) {
                    array[arrayIndex++] = key;
                }
            }
        }

        public struct KeyEnumerator : IEnumerator<LuaValue>
        {
            private Enumerator inner;
            public KeyEnumerator(LuaMap obj) => inner = obj.GetEnumerator();
            public LuaValue Current => inner.Current.Key;
            object IEnumerator.Current => Current;
            void IDisposable.Dispose() { }
            public bool MoveNext() => inner.MoveNext();
            public void Reset() => inner.Reset();
        }

        #endregion

        #region Value collection

        public struct ValueCollection : ICollection<LuaValue>
        {
            private LuaMap obj;
            public ValueCollection(LuaMap obj) {
                this.obj = obj;
            }
            public int Count => obj.Count;
            public bool IsReadOnly => true;
            public void Add(LuaValue item) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public bool Remove(LuaValue item) => throw new NotSupportedException();
            public ValueEnumerator GetEnumerator() => new ValueEnumerator(obj);
            IEnumerator<LuaValue> IEnumerable<LuaValue>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            public bool Contains(LuaValue item) {
                foreach (var value in this) {
                    if (value.Equals(item)) {
                        return true;
                    }
                }
                return false;
            }

            public void CopyTo(LuaValue[] array, int arrayIndex)
            {
                foreach (var value in this) {
                    array[arrayIndex++] = value;
                }
            }
        }

        public struct ValueEnumerator : IEnumerator<LuaValue>
        {
            private Enumerator inner;
            public ValueEnumerator(LuaMap obj) => inner = obj.GetEnumerator();
            public LuaValue Current => inner.Current.Value;
            object IEnumerator.Current => Current;
            void IDisposable.Dispose() { }
            public bool MoveNext() => inner.MoveNext();
            public void Reset() => inner.Reset();
        }

        #endregion
    }
}