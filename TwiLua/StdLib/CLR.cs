using System;

namespace TwiLua.StdLib
{
    public static class LibCLR
    {
        public static Lua LoadCLR(this Lua lua)
        {
            var globals = lua.Globals;
            globals["typeof"] = new LuaCFunction(s => {
                if (s.Count != 1) throw new WrongNumberOfArguments();
                var ud = s[1].ExpectUserdata<IUserdata>("object");
                if (ud is TypeUserdata t) {
                    return s.Return(new ObjectUserdata(t.Type));
                } else if (ud is ObjectUserdata) {
                    throw new Exception("Use `GetType()` to get the Type of a CLR object.");
                } else {
                    throw new Exception($"Expected CLR type, got `{ud}`");
                }
            });
            globals["import"] = new LuaCFunction(s => {
                if (s.Count != 1) throw new WrongNumberOfArguments();
                var name = s[1].ExpectString("typeName");
                var type = Type.GetType(name) ?? throw new Exception($"Type `{name}` not found.");
                return s.Return(TypeUserdata.From(type));
            });
            globals["toClr"] = new LuaCFunction(s => {
                if (s.Count != 2) throw new WrongNumberOfArguments();
                var obj = s[1];
                var type = (s[2].ExpectUserdata<IUserdata>("type").Value as Type) ?? throw new Exception($"Expected CLR type, got `{s[2]}`");
                return s.Return(new ObjectUserdata(obj.ConvertTo(type)));
            });
            globals["fromClr"] = new LuaCFunction(s => {
                if (s.Count != 1) throw new WrongNumberOfArguments();
                var obj = s[1].ExpectUserdata<IUserdata>("object");
                return s.Return(LuaValue.ConvertFrom(obj.Value));
            });
            return lua;
        }
    }
}