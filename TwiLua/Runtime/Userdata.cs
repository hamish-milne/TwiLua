using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TwiLua
{

    public enum ArithmeticOp
    {
        Add,
        Sub,
        Mul,
        Div,
        IDiv,
        Mod,
        Pow,
        BAnd,
        BOr,
        BXor,
        Shl,
        Shr,
    }

    public enum CompareOp
    {
        Eq,
        Lt,
        Le,
    }

    public interface IUserdata
    {
        object? Value { get; }
        LuaValue Index(LuaThread s, in LuaValue key);
        void NewIndex(LuaThread s, in LuaValue key, in LuaValue value);
        LuaValue Arithmetic(LuaThread s, ArithmeticOp op, in LuaValue value);
        bool Compare(LuaThread s, CompareOp op, in LuaValue value);
        LuaValue Unm(LuaThread s);
        int Call(LuaThread s);
        LuaValue Concat(LuaThread s);
    }

    public abstract class Userdata : IUserdata
    {
        public virtual object Value => this;
        public virtual LuaValue Arithmetic(LuaThread s, ArithmeticOp op, in LuaValue value) => throw new NotSupportedException();
        public virtual int Call(LuaThread s) => throw new NotSupportedException();
        public virtual bool Compare(LuaThread s, CompareOp op, in LuaValue value) => throw new NotSupportedException();
        public virtual LuaValue Concat(LuaThread s) => throw new NotSupportedException();
        public virtual LuaValue Index(LuaThread s, in LuaValue key) => throw new NotSupportedException();
        public virtual void NewIndex(LuaThread s, in LuaValue key, in LuaValue value) => throw new NotSupportedException();
        public virtual LuaValue Unm(LuaThread s) => throw new NotSupportedException();
    }

    internal static class ReflectionUtils
    {
        public static int ChooseOverload(LuaThread s, ParameterInfo[][] options, out object?[] args, int offset, string methodName)
        {
            args = new object?[s.Count - offset];
            var candidates = 0;
            var lastIdx = -1;
            for (int i = 0; i < options.Length; i++) {
                if (options[i].Length == args.Length) {
                    candidates++;
                    lastIdx = i;
                }
            }
            if (candidates == 0) {
                throw new Exception($"No overload of {methodName} takes {args.Length} arguments");
            }
            if (candidates == 1) {
                ExpectArgs(s, options[lastIdx], args, offset, methodName);
                return lastIdx;
            }
            for (int i = 0; i < options.Length; i++) {
                if (TryArgs(s, options[i], args, offset)) {
                    return i;
                }
            }
            throw new Exception($"No overload of {methodName} matches the provided arguments");
        }

        public static bool TryArgs(LuaThread s, ParameterInfo[] parameters, object?[] args, int offset)
        {
            if (parameters.Length != args.Length) {
                return false;
            }
            for (var j = 0; j < args.Length; j++) {
                if (!s[j + 1 + offset].TryConvertTo(parameters[j].ParameterType, out args[j])) {
                    return false;
                }
            }
            return true;
        }

        public static void ExpectArgs(LuaThread s, ParameterInfo[] parameters, object?[] args, int offset, string methodName)
        {
            if (parameters.Length != args.Length) {
                throw new Exception($"{methodName} expects {parameters.Length} arguments, got {args.Length}");
            }
            for (var j = 0; j < args.Length; j++) {
                args[j] = s[j + 1 + offset].ConvertTo(parameters[j].ParameterType, s, parameters[j].Name);
            }
        }
    }

    public abstract class ReflectionUserdata : Userdata
    {
        object? FindMember(string key, MethodInfo[] methods, Dictionary<string, object> members)
        {
            var type = GetTargetType();
            var bindingFlags = GetBindingFlags();
            if (members.TryGetValue(key, out var value)) {
                return value;
            }
            var prop = type.GetProperty(key, bindingFlags);
            if (prop != null) {
                members[key] = prop;
                return prop;
            }
            var field = type.GetField(key, bindingFlags);
            if (field != null) {
                members[key] = field;
                return field;
            }
            var foundMethods = methods.Where(m => m.Name == key).ToArray();
            if (foundMethods.Length == 0) {
                return null;
            }
            var ud = foundMethods.Length == 1
                ? (Userdata)new MethodUserdata(foundMethods[0])
                : new MethodGroupUserdata(foundMethods);
            members[key] = ud;
            return ud;
        }

        protected abstract Type GetTargetType();
        protected abstract BindingFlags GetBindingFlags();
        protected abstract (MethodInfo[], Dictionary<string, object>) GetMembersCache();
        protected abstract object? GetSelf();

        protected virtual LuaValue TryIndexerGet(in LuaValue key) => LuaValue.Nil;
        protected virtual bool TryIndexerSet(in LuaValue key, in LuaValue luaValue) => false;

        public override LuaValue Index(LuaThread s, in LuaValue key)
        {
            var (methods, members) = GetMembersCache();
            if (key.String == null) {
                return TryIndexerGet(key);
            }
            var member = FindMember(key.String, methods, members);
            return member switch
            {
                PropertyInfo prop => LuaValue.ConvertFrom(prop.GetValue(GetSelf())),
                FieldInfo field => LuaValue.ConvertFrom(field.GetValue(GetSelf())),
                MethodInfo method => LuaValue.From(method),
                Userdata ud => ud,
                _ => TryIndexerGet(key),
            };
        }

        public override void NewIndex(LuaThread s, in LuaValue key, in LuaValue value)
        {
            var (methods, members) = GetMembersCache();
            var str = key.String;
            if (str == null) {
                if (TryIndexerSet(key, value)) {
                    return;
                }
                throw new Exception($"Cannot index with {key}");
            }
            var member = FindMember(str, methods, members);

            if (member is PropertyInfo prop) {
                prop.SetValue(GetSelf(), value.ConvertTo(prop.PropertyType, s, str));
                return;
            }
            if (member is FieldInfo field) {
                field.SetValue(GetSelf(), value.ConvertTo(field.FieldType, s, str));
                return;
            }
            if (member is Userdata) {
                throw new Exception($"Cannot assign to method");
            }
            if (!TryIndexerSet(key, value)) {
                throw new Exception($"No such property or field");
            }
        }
    }

    public sealed class ObjectUserdata : ReflectionUserdata
    {
        public override object Value { get; }

        public ObjectUserdata(object value)
        {
            Value = value;
        }

        private static readonly Dictionary<Type, (MethodInfo[] methods, (MethodInfo, Type)[] indexGet, (MethodInfo, Type, Type)[] indexSet, Dictionary<string, object> cache)> _cache
            = new();

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        protected override Type GetTargetType() => Value.GetType();
        protected override BindingFlags GetBindingFlags() => flags;
        protected override (MethodInfo[], Dictionary<string, object>) GetMembersCache() {
            var type = Value.GetType();
            if (!_cache.TryGetValue(type, out var members)) {
                var methods = type.GetMethods(flags);
                var indexerName = type.GetCustomAttribute<DefaultMemberAttribute>()?.MemberName;
                var getterName = $"get_{indexerName}";
                var setterName = $"set_{indexerName}";
                var indexGetters = indexerName == null
                    ? Array.Empty<(MethodInfo, Type)>()
                    : methods.Where(m => m.Name == getterName).Select(m => (m, m.GetParameters()[0].ParameterType)).ToArray();
                var indexSetters = indexerName == null
                    ? Array.Empty<(MethodInfo, Type, Type)>()
                    : methods.Where(m => m.Name == setterName).Select(m => {
                        var parameters = m.GetParameters();
                        return (m, parameters[0].ParameterType, parameters[1].ParameterType);
                    }).ToArray();
                members = (methods, indexGetters, indexSetters, new Dictionary<string, object>());
                _cache[type] = members;
            }
            return (members.methods, members.cache);
        }
        protected override object? GetSelf() => Value;

        protected override LuaValue TryIndexerGet(in LuaValue key)
        {
            var indexers = _cache[Value.GetType()].indexGet;
            if (indexers.Length == 0) {
                return LuaValue.Nil;
            }
            foreach (var (method, type) in indexers) {
                if (key.TryConvertTo(type, out var index)) {
                    return LuaValue.ConvertFrom(method.Invoke(Value, new object?[] { index }));
                }
            }
            throw new Exception($"{Value.GetType()} has no suitable indexer for {key}");
        }

        protected override bool TryIndexerSet(in LuaValue key, in LuaValue value)
        {
            var indexers = _cache[Value.GetType()].indexSet;
            if (indexers.Length == 0) {
                return false;
            }
            foreach (var (method, keyType, valueType) in indexers) {
                if (key.TryConvertTo(keyType, out var cKey) && value.TryConvertTo(valueType, out var cValue)) {
                    method.Invoke(Value, new object?[] { cKey, cValue });
                    return true;
                }
            }
            throw new Exception($"{Value.GetType()} has no suitable indexer for [{key}]={value}");
        }

        public override LuaValue Arithmetic(LuaThread s, ArithmeticOp op, in LuaValue value)
        {
            var type = Value.GetType();
            var operationName = op switch
            {
                ArithmeticOp.Add => "op_Addition",
                ArithmeticOp.Sub => "op_Subtraction",
                ArithmeticOp.Mul => "op_Multiply",
                ArithmeticOp.Div => "op_Division",
                ArithmeticOp.IDiv => "op_Division",
                ArithmeticOp.Mod => "op_Modulus",
                ArithmeticOp.Pow => "op_Power",
                ArithmeticOp.BAnd => "op_BitwiseAnd",
                ArithmeticOp.BOr => "op_BitwiseOr",
                ArithmeticOp.BXor => "op_ExclusiveOr",
                ArithmeticOp.Shl => "op_LeftShift",
                ArithmeticOp.Shr => "op_RightShift",
                _ => throw new NotSupportedException(),
            };
            var method = type.GetMethod(operationName, flags);
            if (method != null) {
                return LuaValue.ConvertFrom(method.Invoke(Value, new object?[] { value }));
            }
            throw new Exception($"No such operation {op} for type {type}");
        }

        public override int Call(LuaThread s)
        {
            if (Value is Delegate d) {
                var args = new LuaValue[s.Count];
                for (var i = 0; i < s.Count; i++) {
                    args[i] = s[i + 1];
                }
                var result = d.DynamicInvoke(args);
                if (result != null) {
                    return s.Return(LuaValue.From(result));
                }
                return 0;
            } else {
                return base.Call(s);
            }
        }
    }

    public sealed class MethodUserdata : Userdata
    {
        public override object Value => Method;

        public MethodInfo Method { get; }
        private readonly ParameterInfo[] _parameters;

        public MethodUserdata(MethodInfo method)
        {
            Method = method;
            _parameters = method.GetParameters();
        }

        public override int Call(LuaThread s)
        {
            var offset = Method.IsStatic ? 0 : 1;
            var self = Method.IsStatic ? null : s[1].ConvertTo(Method.DeclaringType!, s, "self");
            var args = new object?[s.Count - offset];
            ReflectionUtils.ExpectArgs(s, _parameters, args, offset, Method.Name);
            var result = Method.Invoke(self, args);
            if (result != null) {
                return s.Return(LuaValue.ConvertFrom(result));
            }
            return 0;
        }
    }

    public sealed class MethodGroupUserdata : Userdata
    {
        public override object Value => _methods[0];

        private readonly MethodInfo[] _methods;
        private readonly ParameterInfo[][] _parameters;

        public MethodGroupUserdata(MethodInfo[] methods)
        {
            _methods = methods;
            _parameters = Array.ConvertAll(methods, m => m.GetParameters());
        }

        public override int Call(LuaThread s)
        {
            var offset = _methods[0].IsStatic ? 0 : 1;
            var self = _methods[0].IsStatic ? null : s[1].ConvertTo(_methods[0].DeclaringType!, s, "self");
            var overloadIdx = ReflectionUtils.ChooseOverload(s, _parameters, out var args, offset, _methods[0].Name);
            var method = _methods[overloadIdx];
            var result = method.Invoke(self, args);
            if (result != null) {
                return s.Return(LuaValue.ConvertFrom(result));
            }
            return 0;
        }
    }

    public sealed class TypeUserdata : ReflectionUserdata
    {
        public override object Value => Type;

        public Type Type { get; }

        private TypeUserdata(Type type)
        {
            Type = type;
            _constructors = type.GetConstructors();
            _methods = type.GetMethods(flags);
            _constructorParameters = Array.ConvertAll(_constructors, c => c.GetParameters());
            _constructorName = $"new {type.Name}";
        }

        private static readonly Dictionary<Type, TypeUserdata> _cache = new();
        private readonly Dictionary<string, object> _members = new();
        private readonly ConstructorInfo[] _constructors;
        private readonly ParameterInfo[][] _constructorParameters;
        private readonly MethodInfo[] _methods;
        private readonly string _constructorName;

        protected override Type GetTargetType() => Type;
        protected override BindingFlags GetBindingFlags() => flags;
        protected override (MethodInfo[], Dictionary<string, object>) GetMembersCache() => (_methods, _members);
        protected override object? GetSelf() => null;

        public static TypeUserdata From(Type type)
        {
            if (!_cache.TryGetValue(type, out var userdata)) {
                userdata = new TypeUserdata(type);
                _cache[type] = userdata;
            }
            return userdata;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;

        public override int Call(LuaThread s)
        {
            if (_constructors.Length == 0) {
                throw new Exception($"Type {Type} has no constructors");
            }
            var overloadIdx = ReflectionUtils.ChooseOverload(s, _constructorParameters, out var args, 0, _constructorName);
            var ctor = _constructors[overloadIdx];
            var result = ctor.Invoke(args)!;
            return s.Return(LuaValue.ConvertFrom(result));
        }
    }
}