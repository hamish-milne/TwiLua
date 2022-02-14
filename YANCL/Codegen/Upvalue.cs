using System;
using System.Collections.Generic;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    public partial class Compiler
    {
        private readonly Compiler? parent;
        private readonly int prototypeIdx;
        private readonly List<UpValueInfo> upValues = new List<UpValueInfo>();

        class TUpvalue : Operand
        {
            public readonly int Index;
            public TUpvalue(int index) => Index = index;
            public override void Load(Compiler c, int dst) => c.Emit(Build2(GETUPVAL, dst, Index));
            public override void Store(Compiler c, int src) => c.Emit(Build2(SETUPVAL, src, Index));
        }

        private int? Upvalue(string name) {
            UpValueInfo upval;
            if (parent != null) {
                var localIdx = parent.Local(name, markUpvalue: true);
                if (localIdx != null) {
                    upval = new UpValueInfo(name, inStack: true, localIdx.Value);
                } else {
                    var parentIdx = parent.Upvalue(name);
                    if (parentIdx != null) {
                        upval = new UpValueInfo(name, inStack: false, parentIdx.Value);
                    } else {
                        return null;
                    }
                }
            } else if (name == "_ENV") {
                upval = new UpValueInfo(name, inStack: true, 0);
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