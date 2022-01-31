using System;
using System.Collections.Generic;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    public partial class Compiler
    {
        class TNewTable : Operand
        {
            public readonly int Index, Instruction;
            public TNewTable(int index, int instruction) {
                Index = index;
                Instruction = instruction;
            }
            public override void Load(Compiler c, int dst) => throw new InvalidOperationException();
            public override int GetR(Compiler c, ref int tmpSlots) => Index;
        }

        class TableIndex : Operand
        {
            public readonly int Table, Indexer;
            public TableIndex(Compiler c, Operand table, Operand indexer) {
                Table = table.GetR(c, ref stackSlots);
                Indexer = indexer.GetRK(c, ref stackSlots);
            }

            public override void Load(Compiler c, int dst) => c.Emit(Build3(GETTABLE, dst, Table, Indexer));
            public override void Store(Compiler c, int src) => c.Emit(Build3(SETTABLE, Table, Indexer, src));
        }

        class UpvalIndex : Operand
        {
            public readonly int Upval, Indexer;
            public UpvalIndex(Compiler c, int upval, Operand indexer) {
                Upval = upval;
                Indexer = indexer.GetRK(c, ref stackSlots);
            }

            public override void Load(Compiler c, int dst) => c.Emit(Build3(GETTABUP, dst, Upval, Indexer));
            public override void Store(Compiler c, int src) => c.Emit(Build3(SETTABUP, Upval, Indexer, src));
        }

        public void Indexee() {
            // var op = Peek(0);
            // if (op is TLocal || op is TUpvalue) {
            //     return;
            // }
            // Pop().Load(this, Top);
            // Push(new TLocal(Top, isVar: false));
        }

        public void Index() {
            var indexer = Pop();
            var table = Pop();
            if (table is TUpvalue upvalue) {
                Push(new UpvalIndex(this, upvalue.Index, indexer));
            } else {
                if (table is TNewTable) {
                    Push(table);
                }
                Push(new TableIndex(this, table, indexer));
            }
        }

        public void NewTable() {
            Push(new TNewTable(PushS(), code.Count));
            Emit(0);
        }

        public void SetList(int array, int hash, bool argPending)
        {
            bool hasDispatch = false;
            if (argPending) {
                hasDispatch = Dispatch(array, 0) == 0;
            }
            if (Pop() is TNewTable op) {
                if (array > 0) {
                    code.Add(Build3(SETLIST, op.Index, hasDispatch ? array-1 : array, 1));
                    Top -= array;
                }
                code[op.Instruction] = Build3(NEWTABLE, op.Index, hasDispatch ? array-1 : array, hash);
                Push(new TLocal(op.Index, isVar: false));
            } else {
                throw new InvalidOperationException();
            }
        }
    }
}