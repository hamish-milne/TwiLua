namespace YANCL
{
    internal static class CompilerExtensions
    {
        public static void Global(this Compiler C, string name) {
            C.Upvalue(0, inStack: true);
            C.Indexee();
            C.Constant(name);
            C.Index();
        }
    }
}