namespace YANCL
{
    public class Lua
    {
        private readonly LuaState state = new LuaState(1024, 256);
        public LuaTable Globals { get; } = new LuaTable();

        public LuaValue[] DoString(string str) {
            return state.Execute(Compile(str), Globals);
        }

        public static LuaFunction Compile(string str) {
            var c = new Parser(str);
            c.ParseChunk();
            return c.MakeFunction();
        }
    }
}