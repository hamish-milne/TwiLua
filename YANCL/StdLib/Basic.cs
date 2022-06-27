using System;

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
                    return s.Return(0);
                case "isrunning":
                    return s.Return(true);
                }
                return 0;
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
                return 0;
            });
            globals["select"] = new LuaCFunction(s => {
                if (s.Count == 0) {
                    throw new WrongNumberOfArguments();
                }
                if (s[1] == "#") {
                    return s.Return(s.Count - 1);
                } else {
                    var idx = s.Integer(1);
                    if (idx <= 0) {
                        throw new ArgumentOutOfRangeException();
                    }
                    if (idx > s.Count - 1) {
                        return s.Return(LuaValue.Nil);
                    } else {
                        return s.Return(s[(int)idx + 1]);
                    }
                }
            });
            var next = new LuaCFunction(s => {
                if (s.Count < 1) {
                    throw new WrongNumberOfArguments();
                }
                var table = s.Table(1);
                var key = s.Count >= 2 ? s[2] : LuaValue.Nil;
                (s[0], s[1]) = table.Next(key) ?? (LuaValue.Nil, LuaValue.Nil);
                return 2;
            });
            globals["next"] = next;
            globals["_G"] = globals;
            globals["_VERSION"] = "Lua 5.4";
            globals["type"] = new LuaCFunction(s => {
                if (s.Count < 1) {
                    throw new WrongNumberOfArguments();
                }
                switch (s[1].Type) {
                case LuaType.NIL: return s.Return("nil");
                case LuaType.BOOLEAN: return s.Return("boolean");
                case LuaType.NUMBER: return s.Return("number");
                case LuaType.STRING: return s.Return("string");
                case LuaType.TABLE: return s.Return("table");
                case LuaType.CFUNCTION:
                case LuaType.FUNCTION:
                    return s.Return("function");
                default:
                    throw new Exception("Unknown type");
                }
            });
            globals["error"] = new LuaCFunction(s => {
                // TODO: Error position
                throw new LuaRuntimeError(s[1]);
            });
            globals["pcall"] = new LuaCFunction(s => {
                if (s.Count < 1) {
                    throw new WrongNumberOfArguments();
                }
                try {
                    var nResults = s.Call(1, s.Count, 1);
                    s[0] = true;
                    return nResults + 1;
                } catch (Exception e) {
                    s.ResetCallStack();
                    s[0] = false;
                    s[1] = e is LuaRuntimeError lerror ? lerror.Value : e.Message;
                    return 2;
                }
            });
            globals["pairs"] = new LuaCFunction(s => {
                if (s.Count < 1) {
                    throw new WrongNumberOfArguments();
                }
                s[0] = next;
                s[2] = LuaValue.Nil;
                return 3;
            });
            globals["ipairs"] = new LuaCFunction(s => {
                if (s.Count == 0) {
                    throw new WrongNumberOfArguments();
                }
                if (s.Count == 1) {
                    s[2] = 0;
                    return 3;
                }
                var table = s.Table(1);
                var idx = s.Integer(2) + 1;
                if (idx > table.ArrayCount) {
                    s[0] = LuaValue.Nil;
                    return 1;
                } else {
                    s[0] = idx;
                    s[1] = table[idx];
                    return 2;
                }
            });
        }
    }
}