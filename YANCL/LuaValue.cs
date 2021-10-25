using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using System.Collections;

namespace YANCL
{
    public enum LuaType {
        NIL = 0,
        BOOLEAN = 1,
        NUMBER = 3,
        STRING = 4,
        TABLE = 5,
        FUNCTION = 6,
        CFUNCTION = 7,
    }

    public sealed class LuaTable : IEnumerable<LuaValue> {
        
        private Dictionary<LuaValue, LuaValue>? Map;
        private List<LuaValue>? Array;

        public LuaValue this[LuaValue key] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (key.Type == LuaType.NUMBER && key.Number == (int)key.Number && key.Number >= 1) {
                    var idx = (int)key.Number - 1;
                    if (idx < Array!.Count) {
                        return Array[idx];
                    } else {
                        return LuaValue.Nil;
                    }
                } else if (Map == null) {
                    return LuaValue.Nil;
                } else {
                    Map!.TryGetValue(key, out var val);
                    return val;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                if (key.Type == LuaType.NUMBER && key.Number == (int)key.Number && key.Number >= 1) {
                    if (Array == null) {
                        Array = new List<LuaValue>();
                    }
                    var idx = (int)key.Number - 1;
                    while (idx >= Array!.Count) {
                        Array.Add(LuaValue.Nil);
                    }
                    Array[idx] = value;
                } else {
                    if (Map == null) {
                        Map = new Dictionary<LuaValue, LuaValue>();
                    }
                    Map[key] = value;
                }
            }
        }

        public IEnumerator<LuaValue> GetEnumerator()
        {
            return ((IEnumerable<LuaValue>?)Array ?? System.Array.Empty<LuaValue>()).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(LuaValue key, LuaValue value) {
            this[key] = value;
        }

        // This prevents the need for explicit casts for standard library definitions
        public void Add(LuaValue key, LuaCFunction value) {
            this[key] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(LuaValue value) {
            this[Count + 1] = value;
        }

        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Array?.Count ?? 0;
        }
    }

    class WrongNumberOfArguments : Exception { }
    class NoIntegerRepresentation : Exception { }

    public delegate void LuaCFunction(LuaCallState s);

    public struct LuaValue : IEquatable<LuaValue> {
        public readonly LuaType Type;
        public readonly double Number;
        public readonly string? String;
        public readonly LuaTable? Table;
        public readonly LuaClosure? Function;
        public readonly LuaCFunction? CFunction;

        public bool Boolean {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                return !(Type == LuaType.NIL || (Type == LuaType.BOOLEAN && Number == 0));
            }
        }

        public int Length {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (Type == LuaType.STRING) {
                    return String!.Length;
                } else if (Type == LuaType.TABLE) {
                    return Table!.Count;
                } else {
                    throw new Exception("Not a string or table");
                }
            }
        }

        public LuaValue this[LuaValue key] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (Table == null) {
                    throw new Exception("Not a table");
                }
                return Table[key];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                if (Table == null) {
                    throw new Exception("Not a table");
                }
                Table[key] = value;
            }
        }

        public static readonly LuaValue Nil = new LuaValue();


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(bool value) {
            Type = LuaType.BOOLEAN;
            Number = value ? 1 : 0;
            String = null;
            Table = null;
            Function = null;
            CFunction = null;
        }

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(double value) {
            Type = LuaType.NUMBER;
            Number = value;
            String = null;
            Table = null;
            Function = null;
            CFunction = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(string value) {
            Type = LuaType.STRING;
            Number = 0;
            String = value;
            Table = null;
            Function = null;
            CFunction = null;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(LuaTable table) {
            Type = LuaType.TABLE;
            Number = 0;
            String = null;
            Table = table;
            Function = null;
            CFunction = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(LuaClosure function) {
            Type = LuaType.FUNCTION;
            Number = 0;
            String = null;
            Table = null;
            Function = function;
            CFunction = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(LuaCFunction function) {
            Type = LuaType.CFUNCTION;
            Number = 0;
            String = null;
            Table = null;
            Function = null;
            CFunction = function;
        }

        public bool Equals(LuaValue other) {
            if (Type != other.Type) return false;
            switch (Type) {
                case LuaType.NIL:
                    return true;
                case LuaType.BOOLEAN:
                case LuaType.NUMBER:
                    return Number == other.Number;
                case LuaType.STRING:
                    return String == other.String;
                case LuaType.TABLE:
                    return Table == other.Table;
                case LuaType.FUNCTION:
                    return Function == other.Function;
                case LuaType.CFUNCTION:
                    return CFunction == other.CFunction;
                default:
                    throw new Exception("Invalid LuaType");
            }
        }

        public override bool Equals(object? obj) {
            return obj is LuaValue other && Equals(other);
        }

        public override int GetHashCode() {
            switch (Type) {
                case LuaType.NIL:
                    return 0;
                case LuaType.BOOLEAN:
                case LuaType.NUMBER:
                    return Number.GetHashCode();
                case LuaType.STRING:
                    return String!.GetHashCode();
                case LuaType.TABLE:
                    return Table!.GetHashCode();
                case LuaType.FUNCTION:
                    return Function!.GetHashCode();
                case LuaType.CFUNCTION:
                    return CFunction!.GetHashCode();
                default:
                    throw new Exception("Invalid LuaType");
            }
        }

        public override string ToString()
        {
            switch (Type) {
                case LuaType.NIL:
                    return "nil";
                case LuaType.BOOLEAN:
                    return Boolean ? "true" : "false";
                case LuaType.NUMBER:
                    return Number.ToString();
                case LuaType.STRING:
                    return String!;
                case LuaType.TABLE:
                    return "<table>";
                case LuaType.CFUNCTION:
                case LuaType.FUNCTION:
                    return "<function>";
                default:
                    throw new Exception("Invalid LuaType");
            }
        }

        public static bool operator ==(LuaValue left, LuaValue right) {
            return left.Equals(right);
        }
        public static bool operator !=(LuaValue left, LuaValue right) {
            return !left.Equals(right);
        }

        public static implicit operator LuaValue(bool value) {
            return new LuaValue(value);
        }

        public static implicit operator LuaValue(double value) {
            return new LuaValue(value);
        }

        public static implicit operator LuaValue(int value) {
            return new LuaValue(value);
        }

        public static implicit operator LuaValue(long value) {
            return new LuaValue(value);
        }

        public static implicit operator LuaValue(string value) {
            return new LuaValue(value);
        }

        public static implicit operator LuaValue(LuaClosure value) {
            return new LuaValue(value);
        }

        public static implicit operator LuaValue(LuaTable value) {
            return new LuaValue(value);
        }

        public static implicit operator LuaValue(LuaCFunction value) {
            return new LuaValue(value);
        }
    }
}
