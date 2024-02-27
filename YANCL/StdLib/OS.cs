using System;

namespace YANCL
{
    public static class OS
    {
        public static void Load(LuaTable globals, bool includeUnsafe = false)
        {
            var startTime = DateTime.Now.Ticks;
            var unsafeFn = new LuaValue(s => {
                throw new LuaRuntimeError("Unsafe functions are not available");
            });
            globals["os"] = new LuaTable
            {
                {"clock", s => s.Return((double)(DateTime.Now.Ticks - startTime) / TimeSpan.TicksPerSecond)},
                {"date", s => {
                    var format = s.Count >= 1 ? s.String(1) : "%c";
                    var time = s.Count >= 2 ? s.Number(2) : (double)DateTime.Now.Ticks / TimeSpan.TicksPerSecond;
                    return s.Return(DateTimeOffset.FromUnixTimeSeconds((long)time).ToString(format));
                }},
                {"difftime", s => s.Return(s.Number(1) - s.Number(2))},
                {"time", s => {
                    if (s.Count == 0) {
                        return s.Return((double)DateTime.Now.Ticks / TimeSpan.TicksPerSecond);
                    } else {
                        var t = s.Table(1).Map;
                        var year = t["year"].As<int>();
                        var month = t["month"].As<int>();
                        var day = t["day"].As<int>();
                        t.TryGetValue("hour", out var hour);
                        t.TryGetValue("min", out var min);
                        t.TryGetValue("sec", out var sec);
                        var dt = new DateTime(year, month, day, (int)hour.Number, (int)min.Number, (int)sec.Number, DateTimeKind.Utc);
                        return s.Return((double)dt.Ticks / TimeSpan.TicksPerSecond);
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