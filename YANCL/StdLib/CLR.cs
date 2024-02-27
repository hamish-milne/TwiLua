using System;
using System.Collections.Generic;
using System.Reflection;

namespace YANCL
{
    public static class CLR
    {
        public static void Load(LuaTable globals, IEnumerable<Assembly> assemblies)
        {
            globals["clr"] = new LuaTable {
                {"typeof", new LuaCFunction(s => {
                    if (s.Count != 1) throw new WrongNumberOfArguments();
                    var ud = s[1].ExpectUserdata();
                    if (ud is TypeUserdata t) {
                        return s.Return(new ObjectUserdata(t.Type));
                    } else if (ud is ObjectUserdata) {
                        throw new Exception("Use `GetType()` to get the Type of a CLR object.");
                    } else {
                        throw new Exception($"Expected CLR type, got `{ud}`");
                    }
                })},
                {"import", new LuaCFunction(s => {
                    var typeName = s[1].ExpectString("typeName");
                    foreach (var a in assemblies) {
                        var type = a.GetType(typeName);
                        if (type != null) {
                            return s.Return(TypeUserdata.From(type));
                        }
                    }
                    throw new Exception($"Type `{typeName}` not found in any assembly.");
                })}
            };
        }

        public static IEnumerable<Assembly> MakeAssemblyList() {
            var domain = AppDomain.CurrentDomain;
            var assemblies = new List<Assembly>();
            foreach (var a in domain.GetAssemblies()) {
                if (a.IsDynamic) continue;
                assemblies.Add(a);
            }
            domain.AssemblyLoad += (sender, args) => {
                if (args.LoadedAssembly.IsDynamic) return;
                assemblies.Add(args.LoadedAssembly);
            };
            return assemblies;
        }
    }
}