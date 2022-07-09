using System;
using System.Collections.Generic;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    public partial class Compiler
    {
        private readonly List<LuaFunction?> closures = new List<LuaFunction?>();

        class TClosure : Operand
        {
            public readonly int Index;
            public TClosure(int index) => Index = index;
            public override void Load(Compiler c, int dst) => c.Emit(Build2x(CLOSURE, dst, Index));
        }

        public Compiler Closure() {
            var idx = closures.Count;
            closures.Add(null);
            Push(new TClosure(idx));
            return new Compiler(this, idx);
        }

        public void EndClosure(Compiler context) {
            closures[context.prototypeIdx] = context.MakeFunction();
        }
    }
}
