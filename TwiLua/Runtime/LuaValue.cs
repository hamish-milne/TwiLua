using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;

namespace TwiLua
{
    public enum LuaType {
        NIL = 0,
        BOOLEAN = 1,
        NUMBER = 3,
        STRING = 4,
        TABLE = 5,
        FUNCTION = 6,
        CFUNCTION = 7,
        THREAD = 8,
        USERDATA = 9,
    }


    class WrongNumberOfArguments : Exception { }
    class NoIntegerRepresentation : Exception { }

    public delegate int LuaCFunction(LuaThread s);

    public readonly partial struct LuaValue : IEquatable<LuaValue>, IComparable<LuaValue> {
        public readonly double Number;
        public readonly object? Object;
        public readonly LuaType Type;
        public readonly int Hash;

        public readonly string? String {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Object as string;
        }

        public readonly LuaTable? Table {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Object as LuaTable;
        }

        public readonly LuaClosure? Function {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Object as LuaClosure;
        }

        public readonly LuaCFunction? CFunction {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Object as LuaCFunction;
        }

        public readonly LuaThread? Thread {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Object as LuaThread;
        }

        public readonly IUserdata? Userdata {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Object as IUserdata;
        }

        public readonly bool Boolean {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                return !(Type == LuaType.NIL || (Type == LuaType.BOOLEAN && Number == 0));
            }
        }

        public readonly int Length {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                return Object switch
                {
                    string String => String.Length,
                    LuaTable Table => Table.Length,
                    _ => throw new Exception("Not a string or table"),
                };

            }
        }

        public readonly LuaValue this[in LuaValue key] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                return Object switch
                {
                    LuaTable Table => Table[key],
                    _ => throw new Exception("Not a table"),
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                if (Object is LuaTable Table) {
                    Table[key] = value;
                } else {
                    throw new Exception("Not a table");
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryGetMetaValue(in LuaValue key, out LuaValue value) {
            var Table = Object as LuaTable;
            if (Table?.MetaTable == null) {
                value = default;
                return false;
            }
            return Table.MetaTable.TryGetValue(key, out value);
        }

        public static readonly LuaValue Nil = new LuaValue();

        private static readonly Dictionary<Type, (Delegate asFunc, Delegate fromFunc)> BuiltInConverters = new Dictionary<Type, (Delegate, Delegate)>();

        private static class Caster<T> {
            public static Func<LuaValue, LuaThread?, T>? As;
            public static Func<T, LuaValue>? From;

            public static bool Exists => As != null && From != null;

            public static void Set(Func<LuaValue, LuaThread?, T> asFunc, Func<T, LuaValue> fromFunc) {
                As = asFunc;
                From = fromFunc;
                BuiltInConverters[typeof(T)] = (asFunc, fromFunc);
            }
        }

        readonly T AssertType<T>(LuaType type, T returns) {
            if (Type != type) {
                throw new Exception($"Expected {type}, got {Type}");
            }
            return returns;
        }

        readonly T AssertDelegate<T>(LuaThread? thread, T returns) {
            if (Type != LuaType.CFUNCTION && Type != LuaType.FUNCTION) {
                throw new Exception($"Expected a function, got {Type}");
            }
            if (thread == null) {
                throw new Exception("A LuaThread must be provided to convert to a delegate");
            }
            return returns;
        }

        static LuaValue() {

            Caster<LuaValue>.Set(
                (v, _) => v,
                (v) => v
            );

            Caster<bool>.Set(
                (v, _) => v.AssertType(LuaType.BOOLEAN, v.Boolean),
                (v) => new LuaValue(v)
            );

            Caster<double>.Set(
                (v, _) => v.AssertType(LuaType.NUMBER, v.Number),
                (v) => new LuaValue(v)
            );

            Caster<int>.Set(
                (v, _) => v.AssertType(LuaType.NUMBER, (int)v.Number),
                (v) => new LuaValue(v)
            );

            Caster<string>.Set(
                (v, _) => v.AssertType(LuaType.STRING, v.Object as string),
                (v) => new LuaValue(v)
            );

            Caster<LuaTable>.Set(
                (v, _) => v.AssertType(LuaType.TABLE, v.Object as LuaTable),
                (v) => new LuaValue(v)
            );

            Caster<LuaMap>.Set(
                (v, _) => v.AssertType(LuaType.TABLE, (v.Object as LuaTable)?.Map),
                (v) => new LuaValue(new LuaTable(null, v))
            );

            Caster<List<LuaValue>>.Set(
                (v, _) => v.AssertType(LuaType.TABLE, (v.Object as LuaTable)?.Array),
                (v) => new LuaValue(new LuaTable(v, null))
            );

            Caster<LuaClosure>.Set(
                (v, _) => v.AssertType(LuaType.FUNCTION, v.Object as LuaClosure),
                (v) => new LuaValue(v)
            );

            Caster<LuaCFunction>.Set(
                (v, _) => v.AssertType(LuaType.CFUNCTION, v.Object as LuaCFunction),
                (v) => new LuaValue(v)
            );

            Caster<Action>.Set(
                (v, s) => v.AssertDelegate<Action>(s, () => {
                    s!.Push(v);
                    s.Call(0, 0);
                }),
                (v) => new LuaValue((s) => {
                    v();
                    return 0;
                })
            );

            Caster<IUserdata>.Set(
                (v, _) => v.AssertType(LuaType.USERDATA, v.Object as IUserdata),
                (v) => new LuaValue(v)
            );
        }

        public T As<T>(LuaThread? thread = null) {
            SetDelegateCaster<T>();
            if (Caster<T>.As == null) {
                throw new Exception("Invalid type: " + typeof(T));
            }
            return Caster<T>.As(this, thread);
        }

        public static LuaValue From<T>(T value) {
            SetDelegateCaster<T>();
            if (Caster<T>.From == null) {
                throw new Exception("Invalid type" + typeof(T));
            }
            return Caster<T>.From(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(bool value) {
            Type = LuaType.BOOLEAN;
            Number = value ? 1 : 0;
            Object = null;
            Hash = value.GetHashCode();
        }

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(double value) {
            Type = LuaType.NUMBER;
            Number = value;
            Object = null;
            Hash = value.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(string value) {
            Type = LuaType.STRING;
            Number = 0;
            Object = value;
            Hash = value.GetHashCode();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(LuaTable table) {
            Type = LuaType.TABLE;
            Number = 0;
            Object = table;
            Hash = table.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(LuaClosure function) {
            Type = LuaType.FUNCTION;
            Number = 0;
            Object = function;
            Hash = function.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(LuaCFunction function) {
            Type = LuaType.CFUNCTION;
            Number = 0;
            Object = function;
            Hash = function.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(LuaThread thread) {
            Type = LuaType.THREAD;
            Number = 0;
            Object = thread;
            Hash = thread.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(IUserdata userdata) {
            Type = LuaType.USERDATA;
            Number = 0;
            Object = userdata;
            Hash = userdata.GetHashCode();
        }

        bool IEquatable<LuaValue>.Equals(LuaValue other) => Equals(other);

        public readonly bool Equals(in LuaValue other) {
            if (Type != other.Type) return false;
            switch (Type) {
                case LuaType.NIL:
                    return true;
                case LuaType.BOOLEAN:
                case LuaType.NUMBER:
                    return Number == other.Number;
                case LuaType.STRING:
                    return (Object as string) == (other.Object as string);
                default:
                    return Object == other.Object;
            }
        }

        public readonly override bool Equals(object? obj) {
            return obj is LuaValue other && Equals(other);
        }

        public readonly override int GetHashCode() => Hash;

        public readonly override string ToString()
        {
            switch (Type) {
                case LuaType.NIL:
                    return "nil";
                case LuaType.BOOLEAN:
                    return Boolean ? "true" : "false";
                case LuaType.NUMBER:
                    return Number.ToString();
                case LuaType.STRING:
                    return (string)Object!;
                case LuaType.TABLE:
                    return "<table>";
                case LuaType.CFUNCTION:
                case LuaType.FUNCTION:
                    return "<function>";
                case LuaType.THREAD:
                    return "<thread>";
                case LuaType.USERDATA:
                    return "<userdata>";
                default:
                    throw new Exception("Invalid LuaType");
            }
        }

        public readonly int CompareTo(LuaValue other)
        {
            if (Type != other.Type) {
                throw new Exception($"Cannot compare {Type} with {other.Type}");
            }
            return Type switch
            {
                LuaType.NUMBER => Number.CompareTo(other.Number),
                LuaType.STRING => ((string)Object!).CompareTo((string)other.Object!),
                _ => throw new Exception($"Cannot compare two {Type} values"),
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in LuaValue left, in LuaValue right) {
            return left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in LuaValue left, in LuaValue right) {
            return !left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator LuaValue(bool value) {
            return new LuaValue(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator LuaValue(double value) {
            return new LuaValue(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator LuaValue(int value) {
            return new LuaValue(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator LuaValue(long value) {
            return new LuaValue(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator LuaValue(string value) {
            return new LuaValue(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator LuaValue(LuaClosure value) {
            return new LuaValue(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator LuaValue(LuaTable value) {
            return new LuaValue(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator LuaValue(LuaCFunction value) {
            return new LuaValue(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator LuaValue(LuaThread value) {
            return new LuaValue(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator LuaValue(Userdata value) {
            return new LuaValue(value);
        }
    }
}
