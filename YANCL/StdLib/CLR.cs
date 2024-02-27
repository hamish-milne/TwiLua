using System;

namespace YANCL
{
    public static class CLR
    {
        public static void Load(LuaTable globals)
        {
            globals["clr"] = new LuaTable {
                {"assemblies", new LuaTable()},
                {"load", new LuaCFunction(s => throw new System.NotImplementedException())},
                {"loadfile", new LuaCFunction(s => throw new System.NotImplementedException())},
                {"loadstring", new LuaCFunction(s => throw new System.NotImplementedException())},
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
                })}
            };
        }
    }
}