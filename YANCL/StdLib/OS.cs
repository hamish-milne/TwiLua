using System;
using System.Text;

namespace YANCL
{
    public static class OS
    {
        static readonly DateTime unixEpochOffset = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        static double FromTimeSpan(TimeSpan timeSpan) => (double)timeSpan.Ticks / TimeSpan.TicksPerSecond;
        static double FromDateTime(DateTime dateTime) => FromTimeSpan(dateTime - unixEpochOffset);
        static DateTime ToDateTime(double time) => unixEpochOffset + TimeSpan.FromSeconds(time);

        static string ConvertFormatString(string str)
        {
            switch (str) {
                case "%c": return "g";
                case "%x": return "d";
                case "%X": return "t";
            }
            var output = new StringBuilder();
            for (var i = 0; i < str.Length; i++)
            {
                output.Append(str[i] switch {
                    'd' or 'f' or 'F' or 'g' or 'h' or 'H' or 'K' or 'm' or 'M' or 's' or 't' or 'y' or 'z' or ':' or '/' => @"\",
                    _ => null
                });
                if (str[i] != '%') {
                    output.Append(str[i]);
                    continue;
                }
                var spec = str[++i] switch {
                    'a' => "ddd",
                    'A' => "dddd",
                    'b' => "MMM",
                    'B' => "MMMM",
                    'd' => "dd",
                    'D' => "MM/dd/yy",
                    'e' => "d",
                    'F' => "yyyy-MM-dd",
                    'h' => "MMM",
                    'H' => "HH",
                    'I' => "hh",
                    'j' => "ddd",
                    'k' => "H",
                    'l' => "h",
                    'm' => "MM",
                    'M' => "mm",
                    'n' => "\n",
                    'p' => "tt",
                    'r' => "hh:mm:ss tt",
                    'R' => "HH:mm",
                    'S' => "ss",
                    't' => "\t",
                    'T' => "HH:mm:ss",
                    'y' => "yy",
                    'Y' => "yyyy",
                    'z' => "zzz",
                    '%' => "%",
                    'c' or 'x' or 'X' => throw new NotSupportedException($"The format %{str[i]} must be used by itself"),
                    _ => throw new NotSupportedException($"The format %{str[i]} is not supported")
                };
                if (output.Length > 0 && output[^1] == spec[0]) {
                    throw new NotSupportedException($"Please add a space between the format %{str[i]} and the previous format");
                }
                output.Append(spec);
            }
            if (output.Length == 1) {
                output.Insert(0, '%');
            }
            return output.ToString();
        }

        public static void Load(LuaTable globals, bool includeUnsafe = false)
        {
            var unsafeFn = new LuaValue(s => {
                throw new LuaRuntimeError("Unsafe functions are not available");
            });
            var startTime = DateTime.Now;

            globals["os"] = new LuaTable
            {
                {"clock", s => s.Return(FromTimeSpan(DateTime.Now - startTime))},
                {"date", s => {
                    var format = s.Count >= 1 ? s.String(1) : "%c";
                    var time = s.Count >= 2 ? ToDateTime(s.Number(2)) : DateTime.Now;
                    return s.Return(time.ToString(ConvertFormatString(format)));
                }},
                {"difftime", s => s.Return(s.Number(1) - s.Number(2))},
                {"time", s => {
                    if (s.Count == 0) {
                        return s.Return(FromDateTime(DateTime.Now));
                    } else {
                        var t = s.Table(1).Map;
                        var year = t["year"].As<int>();
                        var month = t["month"].As<int>();
                        var day = t["day"].As<int>();
                        t.TryGetValue("hour", out var hour);
                        t.TryGetValue("min", out var min);
                        t.TryGetValue("sec", out var sec);
                        return s.Return(FromDateTime(new DateTime(
                            year, month, day,
                            (int)hour.Number, (int)min.Number, (int)sec.Number,
                            DateTimeKind.Utc
                        )));
                    }
                }},
                {"execute", includeUnsafe ? new LuaValue(s => {
                    var cmd = s.String(1);
                    var args = s.Count >= 2 ? s.String(2) : "";
                    var startInfo = new System.Diagnostics.ProcessStartInfo(cmd, args) {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    var process = System.Diagnostics.Process.Start(startInfo);
                    process.WaitForExit();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    s[0] = process.ExitCode;
                    s[1] = output;
                    s[2] = error;
                    return 3;
                }) : unsafeFn},
                {"exit", includeUnsafe ? new LuaValue(s => {
                    var code = s.Count >= 1 ? (int)s.Integer(1) : 0;
                    Environment.Exit(code);
                    return 0;
                }) : unsafeFn},
                {"getenv", s => {
                    var key = s.String(1);
                    var value = Environment.GetEnvironmentVariable(key);
                    return s.Return(value ?? LuaValue.Nil);
                }},
                {"remove", includeUnsafe ? new LuaValue(s => {
                    var path = s.String(1);
                    System.IO.File.Delete(path);
                    return 0;
                }) : unsafeFn},
                {"rename", includeUnsafe ? new LuaValue(s => {
                    var oldPath = s.String(1);
                    var newPath = s.String(2);
                    System.IO.File.Move(oldPath, newPath);
                    return 0;
                }) : unsafeFn},
                {"setlocale", s => {
                    if (s.Count == 0) {
                        return s.Return("C");
                    } else {
                        return s.Return("C");
                    }
                }},
                {"tmpname", includeUnsafe ? new LuaValue(s => {
                    return s.Return(System.IO.Path.GetTempFileName());
                }) : unsafeFn},
            };
        }
    }
}