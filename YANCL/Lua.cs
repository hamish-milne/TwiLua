namespace YANCL
{
    public class Lua
    {
        public const int FieldsPerFlush = 50;

        private readonly LuaState state = new LuaState(1024, 256);
        public LuaTable Globals { get; } = new LuaTable();

        private readonly LuaUpValue[] globalsUpValue;

        public Lua() {
            globalsUpValue = new []{ new LuaUpValue { Value = Globals } };
        }

        public LuaValue[] DoString(string str) {
            return state.Execute(new LuaClosure(Compile(str), globalsUpValue));
        }

        public static LuaFunction Compile(string str, string chunkName = "chunk") {
            return new Parser(new Lexer(str), new Compiler(chunkName)).ParseChunk();
        }
    }
}