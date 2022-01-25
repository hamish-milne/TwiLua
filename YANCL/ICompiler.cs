

namespace YANCL
{
    interface ICompiler
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
        void Call(int arguments);

        void InitLocals(int count, int arguments);
        void AddUpvalue(int index, bool inStack);
        void Assign(int arguments, int targets);
        void SetList(int array, int hash, bool argPending);
        void Discard();
        void Argument();
        void Return(int arguments);
        void Callee();
        // void Indexee();
        void Self();
        
        int Condition();
        int Jump();
        void Mark(int label);

        void SetParameters(int count);
        LuaFunction MakeFunction();
    }
}