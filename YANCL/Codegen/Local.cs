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

        class Scope
        {
            public readonly Scope? Parent;
            public readonly int StartPC;
            public readonly int StartIdx;
            public readonly List<string> Locals = new List<string>();

            public Scope(Scope? parent, int startPC) {
                Parent = parent;
                StartPC = startPC;
                StartIdx = parent?.Locals.Count ?? 0;
            }

            public int? Lookup(string name) {
                for (var i = Locals.Count - 1; i >= 0; i--) {
                    if (Locals[i] == name) return i + StartIdx;
                }
                return Parent?.Lookup(name);
            }
        }

        private Scope? currentScope;
        private readonly List<LocalVarInfo> locals = new List<LocalVarInfo>();

        public void PushScope() => currentScope = new Scope(currentScope, code.Count);

        public void PopScope() {
            foreach (var local in currentScope!.Locals) {
                if (local != "<hidden>") {
                    locals.Add(new LocalVarInfo(local, currentScope.StartPC, code.Count));
                }
            }
            Top -= currentScope.Locals.Count;
            currentScope = currentScope?.Parent;
        }

        public void DefineLocal(string name) {
            if (currentScope!.Locals.Contains(name)) {
                throw new Exception($"local '{name}' already defined");
            }
            currentScope.Locals.Add(name);
        }

        public void Reserve(string name) {
            DefineLocal(name);
            PushS();
        }

        private int? Local(string name) => currentScope?.Lookup(name);

        public void Identifier(string name) {
            var localIdx = Local(name);
            if (localIdx != null) {
                Push(new TLocal(localIdx.Value, isVar: true));
            } else {
                var upval = Upvalue(name);
                if (upval != null) {
                    Push(new TUpvalue(upval.Value));
                } else {
                    Push(new TUpvalue(Upvalue("_ENV")!.Value));
                    Push(new TConstant(name));
                    Index();
                }
            }
        }
    }
}