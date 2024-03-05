using System.IO;

namespace TwiLua.StdLib
{
    public static class LibModules
    {
        public static Lua LoadModules(this Lua lua)
        {
            lua.Globals["package"] = new LuaTable {
                {"config", $"{Path.DirectorySeparatorChar}\n;\n?\n!\n-"},
                {"cpath", ""},
                {"loaded", new LuaTable()},
                {"loadlib", new LuaCFunction(s => throw new System.NotImplementedException())},
                {"path", "?.lua"},
                {"preload", new LuaTable()},
                {"searchpath", new LuaCFunction(s => throw new System.NotImplementedException())},
                {"searchers", new LuaTable {
                    new LuaCFunction(s => throw new System.NotImplementedException()),
                    new LuaCFunction(s => throw new System.NotImplementedException()),
                    new LuaCFunction(s => throw new System.NotImplementedException()),
                    new LuaCFunction(s => throw new System.NotImplementedException())
                }},
                {"seeall", new LuaCFunction(s => throw new System.NotImplementedException())}
            };
            return lua;
        }
    }
}