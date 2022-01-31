using System;
using System.Collections.Generic;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    public partial class Compiler
    {
        private readonly List<LuaFunction> closures = new List<LuaFunction>();

        class TClosure : Operand
        {
            public readonly int Index;
            public TClosure(int index) => Index = index;
            public override void Load(Compiler c, int dst) => c.Emit(Build2(CLOSURE, dst, Index));
        }

        public void Closure(LuaFunction closure) {
            var idx = closures.IndexOf(closure);
            if (idx == -1) {
                idx = closures.Count;
                closures.Add(closure);
            }
            Push(new TClosure(idx));
        }
    }
}
