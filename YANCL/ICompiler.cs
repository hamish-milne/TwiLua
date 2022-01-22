

namespace YANCL
{
    interface ICompiler2
    {
        void Constant(LuaValue value);
        void Vararg();
        void NewTable();
        void Closure(LuaFunction function);
        
        void Local(int idx);
        void Upvalue(int idx);
        void Index();

        void Binary(TokenType token);
        void Unary(TokenType token);
        void Call();

        void InitLocals(int count);
        void Assign();
        void SetList();
        void Discard();
        void Argument();
        void Return();
        
        int Label();
        void Mark(int label);
        void Jump(int label);
        void JumpIf(int label, bool condition);

        LuaFunction MakeFunction();
    }
}