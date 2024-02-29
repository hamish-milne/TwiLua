using System;
using System.Runtime.CompilerServices;

namespace TwiLua
{
    public partial struct LuaValue
    {
        Exception TypeError(string? parameterName, string expectedType) {
            if (parameterName == null) {
                return new Exception($"Expected `{this}` to be a {expectedType}");
            } else {
                return new Exception($"Expected `{this}` to be a {expectedType} for parameter `{parameterName}`");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetNumber(out double number, bool allowCoercion = false) {
            if (IsNumber) {
                number = Number;
                return true;
            } else if (allowCoercion && Object is string s && double.TryParse(s, out number)) {
                return true;
            } else {
                number = 0;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ExpectNumber(string? parameterName = null, bool allowCoercion = false) {
            if (TryGetNumber(out var n, allowCoercion)) {
                return n;
            }
            throw TypeError(parameterName, "number");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetInteger(out long intVal, bool allowCoercion = false) {
            if (TryGetNumber(out var n, allowCoercion) && n % 1 == 0) {
                intVal = (long)n;
                return true;
            } else {
                intVal = 0;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ExpectInteger(string? parameterName = null, bool allowCoercion = false) {
            if (TryGetInteger(out var n, allowCoercion)) {
                return n;
            }
            throw TypeError(parameterName, "integer");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetString(out string str) {
            if (Object is string s) {
                str = s;
                return true;
            } else if (IsNumber) {
                str = Number.ToString();
                return true;
            } else {
                str = "";
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ExpectString(string? parameterName = null) {
            if (TryGetString(out var s)) {
                return s;
            }
            throw TypeError(parameterName, "string");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaTable ExpectTable(string? parameterName = null) {
            if (Object is LuaTable Table) {
                return Table;
            }
            throw TypeError(parameterName, "table");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IUserdata ExpectUserdata(string? parameterName = null) {
            if (Object is IUserdata Userdata) {
                return Userdata;
            }
            throw TypeError(parameterName, "userdata");
        }

        public bool TryConvertTo(Type type, out object? result, LuaThread? thread = null) {
            if (Object is IUserdata ud && ud.Value != null && type.IsAssignableFrom(ud.Value.GetType())) {
                result = ud.Value;
                return true;
            }
            if (!type.IsValueType && Type == TypeTag.Nil) {
                result = null;
                return true;
            }
            if (BuiltInConverters.TryGetValue(type, out var conv)) {
                result = conv.asFunc.DynamicInvoke(this, thread)!;
                return true;
            }
            result = null;
            return false;
        }

        public object ConvertTo(Type type, LuaThread? thread = null, string? parameterName = null) {
            if (TryConvertTo(type, out var result, thread)) {
                return result!;
            }
            throw TypeError(parameterName, type.Name);
        }

        public static LuaValue ConvertFrom(object? value) {
            if (value == null) {
                return Nil;
            }
            var type = value.GetType();
            if (BuiltInConverters.TryGetValue(type, out var conv)) {
                return (LuaValue)conv.fromFunc.DynamicInvoke(value)!;
            }
            return new ObjectUserdata(value);
        }
    }
}