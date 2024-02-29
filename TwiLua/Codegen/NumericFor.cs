using static TwiLua.Instruction;
using static TwiLua.OpCode;

namespace TwiLua
{
    partial class Compiler
    {
        public void ForInit() {
            Argument();
            DefineLocal("(for index)");
            DefineLocal("(for limit)");
            DefineLocal("(for step)");
        }

        public (Label, Label) ForPrep()
        {
            var c0 = Label();
            var c1 = Label();
            Emit(Build2sx(FORPREP, currentScope!.StartIdx, 0));
            Mark(c0);
            JumpAt(c1, code.Count - 1);
            return (c0, c1);
        }

        public void ForLoop((Label, Label) state)
        {
            var (c0, c1) = state;
            Mark(c1);
            Emit(Build2sx(FORLOOP, currentScope!.StartIdx, 0));
            JumpAt(c0, code.Count - 1);
        }
    }
}