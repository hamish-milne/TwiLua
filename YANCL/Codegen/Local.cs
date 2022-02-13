using System;
using System.Collections.Generic;
using System.Linq;
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
            public readonly int StartIdx;
            public readonly List<(string name, int startPC)> Locals = new List<(string, int)>();

            public int Count => Locals.Count + (Parent?.Count ?? 0);

            public Scope(Scope? parent) {
                Parent = parent;
                StartIdx = parent?.Locals.Count ?? 0;
            }

            public int? Lookup(string name) {
                for (var i = Locals.Count - 1; i >= 0; i--) {
                    if (Locals[i].name == name) return i + StartIdx;
                }
                return Parent?.Lookup(name);
            }
        }

        private Scope? currentScope;
        private readonly List<LocalVarInfo> locals = new List<LocalVarInfo>();

        public void PushScope() => currentScope = new Scope(currentScope);

        public void PopScope() {
            for (int i = 0; i < currentScope!.Locals.Count; i++) {
                var local = currentScope.Locals[i];
                // This ensures that locals are defined in the correct order.
                locals.Insert(i, new LocalVarInfo(local.name, local.startPC, code.Count));
            }
            if (Top != currentScope.Count) {
                throw new InvalidOperationException("Stack is not empty");
            }
            Top -= currentScope.Locals.Count;
            currentScope = currentScope?.Parent;
        }

        public void DefineLocal(string name) {
            if (currentScope!.Locals.Any(x => x.name == name)) {
                throw new Exception($"local '{name}' already defined");
            }
            currentScope!.Locals.Add((name, code.Count));
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