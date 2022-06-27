using System;
using System.IO;
using System.Linq;

namespace YANCL.StdLib {
    public static class Basic {
        public static void Load(LuaTable globals) {
            globals["collectgarbage"] = new LuaCFunction(s => {
                switch (s.Count >= 1 ? s.String(1) : "collect") {
                case "collect": GC.Collect(); break;
                case "stop":
                case "restart":
                case "step":
                case "incremental":
                case "generational":
                    // Not supported
                    break;
                case "count":
                    s.Return(0);
                    return;
                case "isrunning":
                    s.Return(true);
                    return;
                }
                s.Count = 0;
            });
            globals["print"] = new LuaCFunction(s => {
                var o = Console.Out;
                for (int i = 1; i <= s.Count; i++) {
                    if (i > 1) {
                        o.Write("\t");
                    }
                    o.Write(s[i].ToString());
                }
                o.WriteLine();
                o.Flush();
                s.Count = 0;
            });
            globals["select"] = new LuaCFunction(s => {
                if (s.Count == 0) {
                    throw new WrongNumberOfArguments();
                }
                if (s[1] == "#") {
                    s.Return(s.Count - 1);
                } else {
                    var idx = s.Integer(1);
                    if (idx <= 0) {
                        throw new ArgumentOutOfRangeException();
                    }
                    if (idx > s.Count - 1) {
                        s.Return(LuaValue.Nil);
                    } else {
                        s.Return(s[(int)idx + 1]);
                    }
                }
            });
            globals["next"] = new LuaCFunction(s => {
                if (s.Count < 1) {
                    throw new WrongNumberOfArguments();
                }
                var table = s.Table(1);
                var key = s.Count >= 2 ? s[2] : LuaValue.Nil;
                LuaValue key2, value;
                if (key == LuaValue.Nil) {
                    (key2, value) = table.Start() ?? (LuaValue.Nil, LuaValue.Nil);
                } else {
                    (key2, value) = table.Next(key) ?? (LuaValue.Nil, LuaValue.Nil);
                }
                s[0] = key2;
                s[1] = value;
                s.Count = 2;
            });
            globals["_G"] = globals;
            globals["_VERSION"] = "Lua 5.4";
            globals["type"] = new LuaCFunction(s => {
                if (s.Count < 1) {
                    throw new WrongNumberOfArguments();
                }
                switch (s[1].Type) {
                case LuaType.NIL: s.Return("nil"); break;
                case LuaType.BOOLEAN: s.Return("boolean"); break;
                case LuaType.NUMBER: s.Return("number"); break;
                case LuaType.STRING: s.Return("string"); break;
                case LuaType.TABLE: s.Return("table"); break;
                case LuaType.CFUNCTION:
                case LuaType.FUNCTION:
                    s.Return("function"); break;
                }
            });
        }
    }
}