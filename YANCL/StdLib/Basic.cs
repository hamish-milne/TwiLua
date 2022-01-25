using System;
using System.IO;

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
        }
    }
}