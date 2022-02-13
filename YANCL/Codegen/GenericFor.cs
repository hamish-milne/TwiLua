using System;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    partial class Compiler
    {
        public (Label, Label) GForInit(int argc) {
            InitLocals(3, argc);
            DefineLocal("(for generator)");
            DefineLocal("(for state)");
            DefineLocal("(for control)");
            var c0 = Label();
            var c1 = Label();
            Jump(c1);
            Mark(c0);
            return (c0, c1);
        }

        public void GForLoop((Label, Label) state, int varCount) {
            var (c0, c1) = state;
            Mark(c1);
            Emit(Build3(TFORCALL, currentScope!.StartIdx, 0, varCount));
            Emit(Build2sx(TFORLOOP, currentScope!.StartIdx + 2, 0));
            JumpAt(c0, code.Count - 1);
        }
    }
}