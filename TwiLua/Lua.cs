namespace TwiLua
{
    public sealed class Lua
    {
        public const int FieldsPerFlush = 50;

        private readonly LuaThread mainThread = new(isMain: true, 1024, 256);
        public LuaTable Globals { get; } = new();

        private readonly LuaUpValue[] globalsUpValue;

        public Lua() {
            globalsUpValue = new []{ new LuaUpValue { Value = Globals } };
        }

        public LuaValue[] DoString(string str) {
            return mainThread.Execute(new LuaClosure(Compile(str), globalsUpValue));
        }

        public static LuaFunction Compile(string str, string chunkName = "chunk") {
            return new Parser(new Lexer(str), new Compiler(chunkName)).ParseChunk();
        }
    }
}