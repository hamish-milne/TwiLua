using System;
using System.Collections.Generic;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    public partial class Compiler
    {
        private readonly List<LuaUpValue> upValues = new List<LuaUpValue>();

        class TUpvalue : Operand
        {
            public readonly int Index;
            public TUpvalue(int index) => Index = index;
            public override void Load(Compiler c, int dst) => c.Emit(Build2(GETUPVAL, dst, Index));
            public override void Store(Compiler c, int src) => c.Emit(Build2(SETUPVAL, src, Index));
        }

        public void Upvalue(int index, bool inStack) {
            var upValue = new LuaUpValue {
                Index = index,
                InStack = inStack
            };
            var idx = upValues.IndexOf(upValue);
            if (idx == -1) {
                idx = upValues.Count;
                upValues.Add(upValue);
            }
            Push(new TUpvalue(idx));
        }
        
    }
}