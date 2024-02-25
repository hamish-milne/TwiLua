using System;
using System.Collections.Generic;
using System.Reflection;

namespace YANCL
{
    public readonly partial struct LuaValue
    {
        static void SetFuncCaster<T>() {
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

        static void SetFuncCaster<T1, T>() {
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

        static void SetFuncCaster<T1, T2, T>() {
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

        static void SetFuncCaster<T1, T2, T3, T>() {
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

        static void SetFuncCaster<T1, T2, T3, T4, T>() {
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
        
        static void SetActionCaster<T1>() {
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

        static void SetActionCaster<T1, T2>() {
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

        static void SetActionCaster<T1, T2, T3>() {
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

        static void SetActionCaster<T1, T2, T3, T4>() {
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
                    { typeof(Func<>), ToMethod(SetFuncCaster<int>) },
                    { typeof(Func<,>), ToMethod(SetFuncCaster<int, int>) },
                    { typeof(Func<,,>), ToMethod(SetFuncCaster<int, int, int>) },
                    { typeof(Func<,,,>), ToMethod(SetFuncCaster<int, int, int, int>) },
                    { typeof(Func<,,,,>), ToMethod(SetFuncCaster<int, int, int, int, int>) },
                    { typeof(Action<>), ToMethod(SetActionCaster<int>) },
                    { typeof(Action<,>), ToMethod(SetActionCaster<int, int>) },
                    { typeof(Action<,,>), ToMethod(SetActionCaster<int, int, int>) },
                    { typeof(Action<,,,>), ToMethod(SetActionCaster<int, int, int, int>) },
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
                method.MakeGenericMethod(type.GetGenericArguments()).Invoke(null, null);
                return true;
            }
            return false;
        }
    }
}