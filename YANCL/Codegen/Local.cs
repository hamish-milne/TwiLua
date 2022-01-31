using System;
using System.Collections.Generic;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    public partial class Compiler
    {
        class TLocal : Operand
        {
            public readonly int Index;
            public TLocal(int index, bool isVar) {
                Index = index;
                stackSlots = isVar ? 0 : 1;
            }
            public override int GetR(Compiler c, ref int tmpSlots) => Index;
            public override void Load(Compiler c, int dst) {
                if (dst == Index) return;
                c.Emit(Build2(MOVE, dst, Index));
            }

            public override void Store(Compiler c, int src) {
                if (src == Index) return;
                c.Emit(Build2(MOVE, Index, src));
            }
        }

        public void Local(int index) => Push(new TLocal(index, isVar: true));
    }
}