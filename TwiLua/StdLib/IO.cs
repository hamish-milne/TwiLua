using System;
using System.IO;

namespace TwiLua.StdLib
{
    public static class IO
    {
        public static Lua LoadIO(this Lua lua) {
            lua.Globals["io"] = new LuaTable {
                {"stdin", new LuaFile(Console.In, null)},
                {"stdout", new LuaFile(null, Console.Out)},
                {"stderr", new LuaFile(null, Console.Error)},
                {"type", s => {
                    if (s[1].Object is LuaFile file) {
                        if (file.IsOpen) {
                            return s.Return("file");
                        } else {
                            return s.Return("closed file");
                        }
                    } else {
                        return s.Return(LuaValue.Fail);
                    }
                }},
                {"open", s => {
                    var path = s.String(1);
                    var mode = s[2] == LuaValue.Nil ? "r" : s.String(2);
                    var binary = mode.EndsWith('b');
                    if (binary) {
                        mode = mode.Substring(0, mode.Length - 1);
                    }
                    return s.Return(mode switch {
                        "r" => new LuaFile(new StreamReader(path), null),
                        "w" => new LuaFile(null, new StreamWriter(path)),
                        "a" => new LuaFile(null, new StreamWriter(path, true)),
                        "r+" => new LuaFile(new FileStream(path, FileMode.Open, FileAccess.ReadWrite)),
                        "w+" => new LuaFile(new FileStream(path, FileMode.Truncate, FileAccess.ReadWrite)),
                        "a+" => new LuaFile(new FileStream(path, FileMode.Append, FileAccess.ReadWrite)),
                        _ => throw new ArgumentException("invalid mode")
                    });
                }},
            };
            return lua;
        }
    }

    public class LuaFile : Userdata
    {
        private TextReader? _reader;
        private TextWriter? _writer;

        public bool IsOpen => _reader != null || _writer != null;

        public LuaFile(TextReader? reader, TextWriter? writer) {
            _reader = reader;
            _writer = writer;
        }

        public LuaFile(Stream stream) {
            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream);
        }

        private static readonly LuaCFunction Read = s => {
            var self = s.Userdata<LuaFile>(1);
            if (self._reader == null) {
                throw new InvalidOperationException("file is not open for reading");
            }
            return s.Return(self._reader.ReadLine());
        };

        private static readonly LuaCFunction Write = s => {
            var self = s.Userdata<LuaFile>(1);
            if (self._writer == null) {
                throw new InvalidOperationException("file is not open for writing");
            }
            self._writer.Write(s.String(2));
            return 0;
        };

        private static readonly LuaCFunction Flush = s => {
            var self = s.Userdata<LuaFile>(1);
            if (self._writer == null) {
                throw new InvalidOperationException("file is not open for writing");
            }
            self._writer.Flush();
            return 0;
        };

        private static readonly LuaCFunction Close = s => {
            var self = s.Userdata<LuaFile>(1);
            if (self._reader != null) {
                self._reader.Dispose();
                self._reader = null;
            }
            if (self._writer != null) {
                self._writer.Dispose();
                self._writer = null;
            }
            return 0;
        };

        public override LuaValue Index(LuaThread s, in LuaValue key)
        {
            return key.ExpectString() switch
            {
                "read" => Read,
                "write" => Write,
                "flush" => Flush,
                "close" => Close,
                _ => LuaValue.Nil
            };
        }
    }
}