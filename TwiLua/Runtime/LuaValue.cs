using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;

namespace TwiLua
{
    public enum TypeTag : byte {
        Nil,
        False,
        True,
        Number,
        Object,
    }


    class WrongNumberOfArguments : Exception { }
    class NoIntegerRepresentation : Exception { }

    public delegate int LuaCFunction(LuaThread s);

    public readonly partial struct LuaValue : IEquatable<LuaValue>, IComparable<LuaValue> {
        public readonly double Number;
        public readonly object? Object;
        public readonly TypeTag Type;

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
                return Type > TypeTag.False;
            }
        }

        public readonly bool IsNumber {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                return Type == TypeTag.Number;
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

        public static readonly LuaValue Nil = default;

        private static readonly Dictionary<Type, (Delegate asFunc, Delegate fromFunc)> BuiltInConverters = new();

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

        readonly T AssertDelegate<T>(LuaThread? thread, T returns) {
            if (Object is not LuaClosure and not LuaCFunction) {
                throw new Exception($"Expected a function, got {this}");
            }
            if (thread == null) {
                throw new Exception("A LuaThread must be provided to convert to a delegate");
            }
            return returns;
        }

        readonly T CastObj<T>() where T : class {
            if (Object is T t) {
                return t;
            }
            throw new Exception($"Expected {typeof(T)}, got {this}");
        }

        readonly T AssertNumber<T>(T returns) {
            if (!IsNumber) {
                throw new Exception($"Expected number, got {this}");
            }
            return returns;
        }

        static LuaValue() {

            Caster<LuaValue>.Set(
                (v, _) => v,
                (v) => v
            );

            Caster<bool>.Set(
                (v, _) => v.Type switch {
                    TypeTag.True => true,
                    TypeTag.False => false,
                    _ => throw new Exception("Expected boolean, got " + v)
                },
                (v) => new LuaValue(v)
            );

            Caster<double>.Set(
                (v, _) => v.AssertNumber(v.Number),
                (v) => new LuaValue(v)
            );

            Caster<float>.Set(
                (v, _) => v.AssertNumber((float)v.Number),
                (v) => new LuaValue(v)
            );

            Caster<int>.Set(
                (v, _) => v.AssertNumber((int)v.Number),
                (v) => new LuaValue(v)
            );

            Caster<long>.Set(
                (v, _) => v.AssertNumber((long)v.Number),
                (v) => new LuaValue(v)
            );

            Caster<short>.Set(
                (v, _) => v.AssertNumber((short)v.Number),
                (v) => new LuaValue(v)
            );

            Caster<byte>.Set(
                (v, _) => v.AssertNumber((byte)v.Number),
                (v) => new LuaValue(v)
            );

            Caster<uint>.Set(
                (v, _) => v.AssertNumber((uint)v.Number),
                (v) => new LuaValue(v)
            );

            Caster<ulong>.Set(
                (v, _) => v.AssertNumber((ulong)v.Number),
                (v) => new LuaValue(v)
            );

            Caster<ushort>.Set(
                (v, _) => v.AssertNumber((ushort)v.Number),
                (v) => new LuaValue(v)
            );

            Caster<sbyte>.Set(
                (v, _) => v.AssertNumber((sbyte)v.Number),
                (v) => new LuaValue(v)
            );

            Caster<decimal>.Set(
                (v, _) => v.AssertNumber((decimal)v.Number),
                (v) => new LuaValue((double)v)
            );

            Caster<string>.Set(
                (v, _) => v.CastObj<string>(),
                (v) => new LuaValue(v)
            );

            Caster<LuaTable>.Set(
                (v, _) => v.CastObj<LuaTable>(),
                (v) => new LuaValue(v)
            );

            Caster<LuaMap>.Set(
                (v, _) => v.CastObj<LuaTable>().Map,
                (v) => new LuaValue(new LuaTable(null, v))
            );

            Caster<List<LuaValue>>.Set(
                (v, _) => v.CastObj<LuaTable>().Array,
                (v) => new LuaValue(new LuaTable(v, null))
            );

            Caster<LuaClosure>.Set(
                (v, _) => v.CastObj<LuaClosure>(),
                (v) => new LuaValue(v)
            );

            Caster<LuaCFunction>.Set(
                (v, _) => v.CastObj<LuaCFunction>(),
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
                (v, _) => v.CastObj<IUserdata>(),
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
            Type = value ? TypeTag.True : TypeTag.False;
            Number = default;
            Object = default;
        }

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(double value) {
            Type = TypeTag.Number;
            Number = value;
            Object = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(string value) {
            Type = TypeTag.Object;
            Number = default;
            Object = value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(LuaTable table) {
            Type = TypeTag.Object;
            Number = default;
            Object = table;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(LuaClosure function) {
            Type = TypeTag.Object;
            Number = default;
            Object = function;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(LuaCFunction function) {
            Type = TypeTag.Object;
            Number = default;
            Object = function;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(LuaThread thread) {
            Type = TypeTag.Object;
            Number = default;
            Object = thread;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaValue(IUserdata userdata) {
            Type = TypeTag.Object;
            Number = default;
            Object = userdata;
        }

        bool IEquatable<LuaValue>.Equals(LuaValue other) => Equals(other);

        public readonly bool Equals(in LuaValue other) {
            if (Type != other.Type) return false;
            return Type switch {
                TypeTag.Object => Object is string str && other.Object is string otherStr
                    ? str == otherStr
                    : Object == other.Object,
                TypeTag.Number => Number == other.Number,
                _ => true
            };
        }

        public readonly override bool Equals(object? obj) {
            return obj is LuaValue other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly override int GetHashCode() => Type switch {
            TypeTag.Object => Object!.GetHashCode(),
            TypeTag.Number => Number.GetHashCode(),
            _ => (int)Type            
        };

        public readonly int Hash {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetHashCode();
        }

        public readonly override string ToString()
        {
            return Type switch
            {
                TypeTag.Nil => "nil",
                TypeTag.True => "true",
                TypeTag.False => "false",
                TypeTag.Number => Number.ToString(),
                _ => Object?.ToString()!,
            };

        }

        public readonly int CompareTo(LuaValue other)
        {
            if (IsNumber && other.IsNumber) {
                return Number.CompareTo(other.Number);
            }
            if (Object is string str && other.Object is string otherStr) {
                return str.CompareTo(otherStr);
            }
            throw new Exception($"Cannot compare {this} with {other}");
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
