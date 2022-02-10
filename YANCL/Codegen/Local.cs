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

        class Scope
        {
            public readonly int Start;
            public readonly List<string> Locals = new List<string>();

            public Scope(int start) {
                Start = start;
            }
        }

        private readonly Stack<Scope> scopes = new Stack<Scope>();

        public void PushScope() => scopes.Push(new Scope(code.Count));

        public void PopScope() {}
    }
}