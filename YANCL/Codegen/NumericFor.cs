using System;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    partial class Compiler
    {
        public int ForPrep(Label c0, Label c1)
        {
            Argument();
            Emit(Build2sx(FORPREP, Top - 3, 0));
            Mark(c0);
            JumpAt(c1, code.Count - 1);
            return Top++;
        }

        public void ForLoop(Label c0, Label c1)
        {
            Top--;
            Mark(c1);
            Emit(Build2sx(FORLOOP, Top - 3, 0));
            JumpAt(c0, code.Count - 1);
            Top -= 3;
        }
    }
}