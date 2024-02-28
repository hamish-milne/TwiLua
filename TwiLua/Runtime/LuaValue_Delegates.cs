using System;
using System.Collections.Generic;
using System.Reflection;

namespace TwiLua
{
    public readonly partial struct LuaValue
    {
        public static void SetCasterFunc<T>() {
            Caster<Func<T>>.Set(
                (v, s) => v.AssertDelegate<Func<T>>(s, () => {
                    s!.Push(v);
                    s.Call(0, 1);
                    return s.Pop().As<T>();
                }),
                (v) => new LuaValue((s) => {
                    return s.Return(From(v()));
                })
            );
        }

        public static void SetCasterFunc<T1, T>() {
            Caster<Func<T1, T>>.Set(
                (v, s) => v.AssertDelegate<Func<T1, T>>(s, (a1) => {
                    s!.Push(v);
                    s.Push(From(a1));
                    s.Call(1, 1);
                    return s.Pop().As<T>();
                }),
                (v) => new LuaValue((s) => {
                    return s.Return(From(v(s[1].As<T1>())));
                })
            );
        }

        public static void SetCasterFunc<T1, T2, T>() {
            Caster<Func<T1, T2, T>>.Set(
                (v, s) => v.AssertDelegate<Func<T1, T2, T>>(s, (a1, a2) => {
                    s!.Push(v);
                    s.Push(From(a1));
                    s.Push(From(a2));
                    s.Call(2, 1);
                    return s.Pop().As<T>();
                }),
                (v) => new LuaValue((s) => {
                    return s.Return(From(v(s[1].As<T1>(), s[2].As<T2>())));
                })
            );
        }

        public static void SetCasterFunc<T1, T2, T3, T>() {
            Caster<Func<T1, T2, T3, T>>.Set(
                (v, s) => v.AssertDelegate<Func<T1, T2, T3, T>>(s, (a1, a2, a3) => {
                    s!.Push(v);
                    s.Push(From(a1));
                    s.Push(From(a2));
                    s.Push(From(a3));
                    s.Call(3, 1);
                    return s.Pop().As<T>();
                }),
                (v) => new LuaValue((s) => {
                    return s.Return(From(v(s[1].As<T1>(), s[2].As<T2>(), s[3].As<T3>())));
                })
            );
        }

        public static void SetCasterFunc<T1, T2, T3, T4, T>() {
            Caster<Func<T1, T2, T3, T4, T>>.Set(
                (v, s) => v.AssertDelegate<Func<T1, T2, T3, T4, T>>(s, (a1, a2, a3, a4) => {
                    s!.Push(v);
                    s.Push(From(a1));
                    s.Push(From(a2));
                    s.Push(From(a3));
                    s.Push(From(a4));
                    s.Call(4, 1);
                    return s.Pop().As<T>();
                }),
                (v) => new LuaValue((s) => {
                    return s.Return(From(v(s[1].As<T1>(), s[2].As<T2>(), s[3].As<T3>(), s[4].As<T4>())));
                })
            );
        }
        
        public static void SetCasterAction<T1>() {
            Caster<Action<T1>>.Set(
                (v, s) => v.AssertDelegate<Action<T1>>(s, (a1) => {
                    s!.Push(v);
                    s.Push(From(a1));
                    s.Call(1, 0);
                }),
                (v) => new LuaValue((s) => {
                    v(s[1].As<T1>());
                    return 0;
                })
            );
        }

        public static void SetCasterAction<T1, T2>() {
            Caster<Action<T1, T2>>.Set(
                (v, s) => v.AssertDelegate<Action<T1, T2>>(s, (a1, a2) => {
                    s!.Push(v);
                    s.Push(From(a1));
                    s.Push(From(a2));
                    s.Call(2, 0);
                }),
                (v) => new LuaValue((s) => {
                    v(s[1].As<T1>(), s[2].As<T2>());
                    return 0;
                })
            );
        }

        public static void SetCasterAction<T1, T2, T3>() {
            Caster<Action<T1, T2, T3>>.Set(
                (v, s) => v.AssertDelegate<Action<T1, T2, T3>>(s, (a1, a2, a3) => {
                    s!.Push(v);
                    s.Push(From(a1));
                    s.Push(From(a2));
                    s.Push(From(a3));
                    s.Call(3, 0);
                }),
                (v) => new LuaValue((s) => {
                    v(s[1].As<T1>(), s[2].As<T2>(), s[3].As<T3>());
                    return 0;
                })
            );
        }

        public static void SetCasterAction<T1, T2, T3, T4>() {
            Caster<Action<T1, T2, T3, T4>>.Set(
                (v, s) => v.AssertDelegate<Action<T1, T2, T3, T4>>(s, (a1, a2, a3, a4) => {
                    s!.Push(v);
                    s.Push(From(a1));
                    s.Push(From(a2));
                    s.Push(From(a3));
                    s.Push(From(a4));
                    s.Call(4, 0);
                }),
                (v) => new LuaValue((s) => {
                    v(s[1].As<T1>(), s[2].As<T2>(), s[3].As<T3>(), s[4].As<T4>());
                    return 0;
                })
            );
        }

        static MethodInfo ToMethod(Action action) => action.Method.GetGenericMethodDefinition();

        static Dictionary<Type, MethodInfo> SetDelegateCasterMethods = null!;

        static void CreateDelegateCasterMethodsCache() {
            if (SetDelegateCasterMethods != null) {
                return;
            }
            try {
                SetDelegateCasterMethods = new Dictionary<Type, MethodInfo>() {
                    { typeof(Func<>), ToMethod(SetCasterFunc<int>) },
                    { typeof(Func<,>), ToMethod(SetCasterFunc<int, int>) },
                    { typeof(Func<,,>), ToMethod(SetCasterFunc<int, int, int>) },
                    { typeof(Func<,,,>), ToMethod(SetCasterFunc<int, int, int, int>) },
                    { typeof(Func<,,,,>), ToMethod(SetCasterFunc<int, int, int, int, int>) },
                    { typeof(Action<>), ToMethod(SetCasterAction<int>) },
                    { typeof(Action<,>), ToMethod(SetCasterAction<int, int>) },
                    { typeof(Action<,,>), ToMethod(SetCasterAction<int, int, int>) },
                    { typeof(Action<,,,>), ToMethod(SetCasterAction<int, int, int, int>) },
                };
            } catch {
                // Reflection is disabled
                SetDelegateCasterMethods = new Dictionary<Type, MethodInfo>();
            }
        }

        static bool SetDelegateCaster<T>() {
            if (Caster<T>.Exists) {
                return true;
            }
            if (!typeof(T).IsGenericType) {
                return false;
            }
            var type = typeof(T).GetGenericTypeDefinition();
            CreateDelegateCasterMethodsCache();
            if (SetDelegateCasterMethods.TryGetValue(type, out var method)) {
                try {
                    #pragma warning disable IL3050 // We're OK with a runtime error here
                    method.MakeGenericMethod(typeof(T).GetGenericArguments()).Invoke(null, null);
                    return true;
                } catch (Exception e) {
                    throw new Exception($"AOT error: please call LuaValue.SetCaster{type}() manually during initialization", e);
                }
            }
            return false;
        }
    }
}