using System.Text;

namespace YANCL.StdLib {
    public static class Table {
        public static void Load(LuaTable globals) {
            globals["table"] = new LuaTable {
                {"concat", s => {
                    var sb = new StringBuilder();
                    var list = s.Table(1);
                    var sep = s.Count >= 2 ? s.String(2) : "";
                    var i = s.Count >= 3 ? s.Integer(3) : 1;
                    var i1 = i;
                    var j = s.Count >= 4 ? s.Integer(4) : list.Count;

                    for (; i <= j; i++) {
                        if (i != i1) {
                            sb.Append(sep);
                        }
                        sb.Append(list[i]);
                    }
                    return s.Return(sb.ToString());
                }},
                {"insert", s => {
                    var list = s.Table(1);
                    var pos = s.Count >= 3 ? s.Integer(2) : list.Count + 1;
                    var value = s.Count >= 3 ? s[3] : s[2];
                    list.Insert((int)pos - 1, value);
                    return 0;
                }},
                {"move", s => {
                    var a1 = s.Table(1);
                    var f = (int)s.Integer(2);
                    var e = (int)s.Integer(3);
                    var t = (int)s.Integer(4);
                    var a2 = s.Count >= 5 ? s.Table(5) : a1;

                    for (int i = 0; i <= (e - f); i++) {
                        a2[t + i] = a1[f + i];
                    }
                    return 0;
                }},
                {"pack", s => {
                    var list = new LuaTable();
                    for (int i = 1; i <= s.Count; i++) {
                        list.Add(s[i]);
                    }
                    list["n"] = s.Count;
                    return s.Return(list);
                }},
                {"remove", s => {
                    var list = s.Table(1);
                    var pos = s.Count >= 2 ? s.Integer(2) : list.Count;

                    return s.Return(list.RemoveAt((int)pos - 1));
                }},
                {"unpack", s => {
                    var list = s.Table(1);
                    var i = s.Count >= 2 ? s.Integer(2) : 1;
                    var j = s.Count >= 3 ? s.Integer(3) : list.Count;

                    int k;
                    for (k = 0; i <= list.Count && i <= j; k++, i++) {
                        s[k] = list[i];
                    }
                    return k;
                }}
            };
        }
    }
}