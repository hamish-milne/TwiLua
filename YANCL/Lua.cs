namespace YANCL
{
    public class Lua
    {
        private readonly LuaState state = new LuaState(1024, 256);
        public LuaTable Globals { get; } = new LuaTable();

        private readonly LuaUpValue[] globalsUpValue;

        public Lua() {
            globalsUpValue = new []{ new LuaUpValue { Value = Globals } };
        }

        public LuaValue[] DoString(string str) {
            return state.Execute(new LuaClosure(Compile(str), globalsUpValue));
        }

        public static LuaFunction Compile(string str) {
            var c = new Parser(str);
            c.ParseChunk();
            return c.MakeFunction();
        }
    }
}