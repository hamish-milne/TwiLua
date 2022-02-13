using System;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    partial class Compiler
    {
        public void ForInit() {
            Argument();
            for (int i = 0; i < 3; i++) {
                Reserve("<hidden>");
            }
        }

        public void ForPrep(Label c0, Label c1)
        {
            Emit(Build2sx(FORPREP, Top - currentScope!.Locals.Count, 0));
            Mark(c0);
            JumpAt(c1, code.Count - 1);
        }

        public void ForLoop(Label c0, Label c1)
        {
            Mark(c1);
            Emit(Build2sx(FORLOOP, Top - currentScope!.Locals.Count, 0));
            JumpAt(c0, code.Count - 1);
        }
    }
}