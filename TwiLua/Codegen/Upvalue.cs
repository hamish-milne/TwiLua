using System.Collections.Generic;
using static TwiLua.Instruction;
using static TwiLua.OpCode;

namespace TwiLua
{
    partial class Compiler
    {
        private readonly Compiler? parent;
        private readonly int prototypeIdx;
        private readonly List<UpValueInfo> upValues = new();

        class TUpvalue : Operand
        {
            public int Index { get; private set; }
            public TUpvalue Init(int index) {
                Index = index;
                return this;
            }
            public override void Load(Compiler c, int dst) => c.Emit(Build2(GETUPVAL, dst, Index));
            public override void Store(Compiler c, int src) => c.Emit(Build2(SETUPVAL, src, Index));
        }

        private int? Upvalue(string name) {
            UpValueInfo upval;
            if (parent != null) {
                var localIdx = parent.Local(name, markUpvalue: true);
                if (localIdx != null) {
                    upval = new(name, inStack: true, localIdx.Value);
                } else {
                    var parentIdx = parent.Upvalue(name);
                    if (parentIdx != null) {
                        upval = new(name, inStack: false, parentIdx.Value);
                    } else {
                        return null;
                    }
                }
            } else if (name == "_ENV") {
                upval = new(name, inStack: true, 0);
            } else {
                return null;
            }
            var idx = upValues.IndexOf(upval);
            if (idx < 0) {
                idx = upValues.Count;
                upValues.Add(upval);
            }
            return idx;
        }
        
    }
}