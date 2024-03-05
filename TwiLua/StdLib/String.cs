using System.Text;

namespace TwiLua.StdLib
{
    public static class LibString
    {
        public static Lua LoadString(this Lua lua)
        {
            lua.Globals["string"] = new LuaTable {
                {"byte", s => {
                    var t = s.String(1);
                    var i = 1;
                    var j = 1;
                    switch (s.Count) {
                        case 1: break;
                        case 2: i = (int)s.Integer(2); break;
                        case 3: i = (int)s.Integer(2); j = (int)s.Integer(3); break;
                        default: throw new WrongNumberOfArguments();
                    }
                    i--;
                    j--;
                    var c = 0;
                    for (; i <= j && i < t.Length; i++) {
                        s[c++] = (int)t[i];
                    }
                    return c;
                }},
                {"char", s => {
                    var buf = new char[s.Count];
                    for (int i = 1; i <= s.Count; i++) {
                        buf[i-1] = (char)s.Integer(i);
                    }
                    return s.Return(new string(buf));
                }},
                {"len", s => s.Return(s.String().Length)},
                {"reverse", s => {
                    var buf = s.String().ToCharArray();
                    System.Array.Reverse(buf);
                    return s.Return(new string(buf));
                }},
                {"rep", s => {
                    var sb = new StringBuilder();
                    var t = s.String(1);
                    var n = s.Integer(2);
                    var sep = s.Count >= 3 ? s.String(3) : "";
                    for (int i = 0; i < n; i++) {
                        if (i > 0) {
                            sb.Append(sep);
                        }
                        sb.Append(t);
                    }
                    return s.Return(sb.ToString());
                }},
                {"lower", s => s.Return(s.String().ToLowerInvariant())},
                {"upper", s => s.Return(s.String().ToUpperInvariant())},
            };
            return lua;
        }
    }

}