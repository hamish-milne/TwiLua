#nullable enable
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;

namespace YANCL
{
    public enum LuaType {
        NIL = 0,
        BOOLEAN = 1,
        NUMBER = 3,
        STRING = 4,
        TABLE = 5,
        FUNCTION = 6,
    }

    public struct LuaValue : IEquatable<LuaValue> {
        public readonly LuaType Type;
        public readonly double Number;
        public readonly string? String;
        public readonly Dictionary<LuaValue, LuaValue>? Table;
        public readonly List<LuaValue>? Array;
        public readonly LuaClosure? Function;
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
                    return Array!.Count;
                } else {
                    throw new Exception("Not a string or table");
                }
            }
        }

        public LuaValue this[LuaValue key] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (Type != LuaType.TABLE) {
                    throw new Exception("Not a table");
                }
                if (key.Type == LuaType.NUMBER && key.Number == (int)key.Number && key.Number >= 1) {
                    var idx = (int)key.Number - 1;
                    if (idx < Array!.Count) {
                        return Array[idx];
                    } else {
                        return LuaValue.Nil;
                    }
                } else {
                    Table!.TryGetValue(key, out var val);
                    return val;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                if (Type != LuaType.TABLE) {
                    throw new Exception("Not a table");
                }
                if (key.Type == LuaType.NUMBER && key.Number == (int)key.Number && key.Number >= 1) {
                    var idx = (int)key.Number - 1;
                    while (idx >= Array!.Count) {
                        Array.Add(LuaValue.Nil);
                    }
                    Array[idx] = value;
                } else {
                    Table![key] = value;
                }
            }
        }

        public static LuaValue Nil = new LuaValue();


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(bool value) {
            Type = LuaType.BOOLEAN;
            Number = value ? 1 : 0;
            String = null;
            Table = null;
            Array = null;
            Function = null;
        }

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(double value) {
            Type = LuaType.NUMBER;
            Number = value;
            String = null;
            Table = null;
            Array = null;
            Function = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(string value) {
            Type = LuaType.STRING;
            Number = 0;
            String = value;
            Table = null;
            Array = null;
            Function = null;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(Dictionary<LuaValue, LuaValue> table, List<LuaValue> array) {
            Type = LuaType.TABLE;
            Number = 0;
            String = null;
            Table = table;
            Array = array;
            Function = null;
        }

        public static LuaValue NewTable() {
            return new LuaValue(new Dictionary<LuaValue, LuaValue>(), new List<LuaValue>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(LuaClosure function) {
            Type = LuaType.FUNCTION;
            Number = 0;
            String = null;
            Table = null;
            Array = null;
            Function = function;
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
    }
}
