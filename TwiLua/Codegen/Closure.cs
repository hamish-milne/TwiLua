using System.Collections.Generic;
using static TwiLua.Instruction;
using static TwiLua.OpCode;

namespace TwiLua
{
    partial class Compiler
    {
        private readonly List<LuaFunction?> closures = new();

        class TClosure : Operand
        {
            public int Index { get; private set; }
            public TClosure Init(int index) {
                Index = index;
                return this;
            }
            public override void Load(Compiler c, int dst) => c.Emit(Build2x(CLOSURE, dst, Index));
        }

        public Compiler Closure() {
            var idx = closures.Count;
            closures.Add(null);
            Push<TClosure>().Init(idx);
            return new Compiler(this, idx);
        }

        public void EndClosure(Compiler context) {
            closures[context.prototypeIdx] = context.MakeFunction();
        }
    }
}
