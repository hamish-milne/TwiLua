using System;
using static TwiLua.Instruction;
using static TwiLua.OpCode;

namespace TwiLua
{
    partial class Compiler
    {
        class TNewTable : Operand
        {
            public int Index { get; private set; }
            public int Instruction { get; private set; }
            public TNewTable Init(int index, int instruction) {
                Index = index;
                Instruction = instruction;
                return this;
            }
            public override void Load(Compiler c, int dst) => throw new InvalidOperationException();
            public override int GetR(Compiler c, ref int tmpSlots) => Index;
        }

        class TableIndex : OperandWithSlots
        {
            public int Table { get; private set; }
            public int Indexer { get; private set; }
            public TableIndex Init(Compiler c, Operand table, Operand indexer) {
                stackSlots = 0;
                Table = table.GetR(c, ref stackSlots);
                Indexer = indexer.GetRK(c, ref stackSlots);
                return this;
            }

            public override void Load(Compiler c, int dst) => c.Emit(Build3(GETTABLE, dst, Table, Indexer));
            public override void Store(Compiler c, int src) => c.Emit(Build3(SETTABLE, Table, Indexer, src));
        }

        class UpvalIndex : OperandWithSlots
        {
            public int Upval { get; private set; }
            public int Indexer { get; private set; }
            public UpvalIndex Init(Compiler c, int upval, Operand indexer) {
                stackSlots = 0;
                Upval = upval;
                Indexer = indexer.GetRK(c, ref stackSlots);
                return this;
            }

            public override void Load(Compiler c, int dst) => c.Emit(Build3(GETTABUP, dst, Upval, Indexer));
            public override void Store(Compiler c, int src) => c.Emit(Build3(SETTABUP, Upval, Indexer, src));
        }

        public void Indexee() {
            // var op = Peek(0);
            // if (op is TLocal || op is TUpvalue) {
            //     return;
            // }
            // PopAndRelease().Load(this, Top);
            // Push<TLocal>().Init(PushS(), isVar: false);
        }

        public void Index() {
            var indexer = Pop();
            if (Peek(0) is TUpvalue upvalue) {
                PopAndRelease();
                Push<UpvalIndex>().Init(this, upvalue.Index, indexer);
            } else {
                var table = Peek(0);
                if (table is not TNewTable) {
                    PopAndRelease();
                }
                Push<TableIndex>().Init(this, table, indexer);
            }
            Release(indexer);
        }

        public void NewTable() {
            Push<TNewTable>().Init(PushS(), code.Count);
            Emit(0);
        }

        public void SetList(int array, int hash, bool argPending)
        {
            bool hasDispatch = false;
            if (argPending) {
                hasDispatch = Dispatch(array, 0) == 0;
            }
            var nArray = hasDispatch ? array-1 : array;
            if (PopAndRelease() is TNewTable op) {
                if (array > 0) {
                    code.Add(Build3(SETLIST, op.Index, nArray % Lua.FieldsPerFlush, (array / Lua.FieldsPerFlush) + 1));
                    Top -= array % Lua.FieldsPerFlush;
                }
                code[op.Instruction] = Build3(NEWTABLE, op.Index, ToFPByte(nArray), ToFPByte(hash));
                Push<TLocal>().Init(op.Index, isVar: false);
            } else {
                throw new InvalidOperationException();
            }
        }

        public void FlushTable(int array)
        {
            Argument();
            if (Peek(0) is TNewTable op) {
                code.Add(Build3(SETLIST, op.Index, Lua.FieldsPerFlush, ((array-1) / Lua.FieldsPerFlush) + 1));
                Top -= Lua.FieldsPerFlush;
            } else {
                throw new InvalidOperationException();
            }
        }
    }
}