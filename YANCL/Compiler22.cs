using System;
using System.Collections.Generic;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    sealed class Compiler22 : ICompiler2
    {

        enum OperandType {
            Nil,
            Local,
            Constant,
            Upvalue,
            Call,
            Vararg,
            Expression
        }

        struct Operand {
            public OperandType Type;
            public int A, B;
            public LuaValue Value;
        }

        private int K(LuaValue value) {
            var idx = constants.IndexOf(value);
            if (idx == -1) {
                constants.Add(value);
                idx = constants.Count - 1;
            }
            return idx;
        }

        private int LoadInst(Operand op, int dst) {
            switch (op.Type) {
                case OperandType.Nil:
                    return Build2(LOADNIL, dst, 0);
                case OperandType.Local:
                    return Build2(MOVE, dst, op.A);
                case OperandType.Upvalue:
                    return Build2(GETUPVAL, dst, op.A);
                case OperandType.Constant:
                    return Build2x(LOADK, dst, K(op.Value));
                case OperandType.Expression:
                    return Build2x(GetOpCode(op.A), dst, GetBx(op.A));
                case OperandType.Vararg:
                    return Build2(VARARG, dst, 1);
                default:
                    throw new System.NotImplementedException(op.Type.ToString());
            }
        }

        private int StoreInst(Operand op, int src) {
            switch (op.Type) {
                case OperandType.Local:
                    return Build2(MOVE, op.A, src);
                case OperandType.Upvalue:
                    return Build2(SETUPVAL, op.A, src);
                case OperandType.Expression:
                    if (op.B == -1) {
                        throw new System.NotImplementedException("Assignment to expression");
                    }
                    return Build3(GetOpCode(op.B), GetA(op.B), GetB(op.B), src);
                default:
                    throw new System.NotImplementedException(op.Type.ToString());
            }
        }

        private readonly List<LuaValue> constants = new List<LuaValue>();
        private readonly List<int> code = new List<int>();
        private readonly List<Operand> operands = new List<Operand>();

        private int top;
        private int arguments;
        private int maxStack;

        private int PushS() {
            var r = top++;
            maxStack = Math.Max(maxStack, top);
            return r;
        }

        public void Constant(LuaValue value)
        {
            if (value == LuaValue.Nil) {
                operands.Add(new Operand {
                    Type = OperandType.Nil
                });
            } else {
                operands.Add(new Operand {
                    Type = OperandType.Constant,
                    Value = value,
                });
            }
        }

        public void Argument()
        {
            var op = Pop();
            code.Add(LoadInst(op, PushS()));
            arguments++;
        }

        public void Assign()
        {
            var target = Peek(1);
            code.Add(StoreInst(target, PopRK()));
            Pop();
        }

        public void Binary(TokenType token)
        {
            if (Peek(0).Value.Type == LuaType.NUMBER &&
                Peek(1).Value.Type == LuaType.NUMBER
            ) {
                var b = Pop().Value.Number;
                var a = Pop().Value.Number;
                double? result = token switch {
                    TokenType.Plus => a + b,
                    TokenType.Minus => a - b,
                    TokenType.Star => a * b,
                    TokenType.Slash => a / b,
                    TokenType.Percent => a % b,
                    TokenType.Caret => System.Math.Pow(a, b),
                    _ => null
                };
                operands.Add(new Operand {
                    Type = OperandType.Constant,
                    Value = new LuaValue(result.Value),
                });
                return;
            } else {
                var inst = token switch {
                    TokenType.Plus => ADD,
                    TokenType.Minus => SUB,
                    TokenType.Star => MUL,
                    TokenType.Slash => DIV,
                    TokenType.Percent => MOD,
                    TokenType.Caret => POW,
                    _ => throw new System.NotImplementedException()
                };
                var b = PopRK();
                var a = PopRK();
                operands.Add(new Operand {
                    Type = OperandType.Expression,
                    A = Build3(inst, 0, a, b),
                    B = -1,
                });
            }
        }

        public void Call()
        {
            operands.Add(new Operand {
                Type = OperandType.Call,
                A = PopRK(),
                B = arguments,
            });
            arguments = 0;
        }

        public void Discard()
        {
            if (Peek(0).Type != OperandType.Call) {
                throw new InvalidOperationException();
            }
            var op = Pop();
            code.Add(Build3(CALL, op.A, op.B, 1));
        }

        private int PopRK()
        {
            var op = Pop();
            switch (op.Type) {
                case OperandType.Nil:
                    return K(LuaValue.Nil);
                case OperandType.Local:
                    return op.A;
                case OperandType.Constant:
                    return K(op.Value) | KFlag;
                default:
                    var slot = PushS();
                    code.Add(LoadInst(op, slot));
                    return slot;
            }
        }

        Operand Pop() {
            var op = operands[operands.Count - 1];
            operands.RemoveAt(operands.Count - 1);
            return op;
        }

        Operand Peek(int index) {
            return operands[operands.Count - index - 1];
        }

        public void Index()
        {
            var indexer = PopRK();
            if (Peek(1).Type == OperandType.Upvalue) {
                operands.Add(new Operand {
                    Type = OperandType.Expression,
                    A = Build3(GETTABUP, 0, Peek(0).A, indexer),
                    B = Build3(SETTABUP, Peek(0).A, indexer, 0),
                });
            } else {
                var table = PopRK();
                operands.Add(new Operand {
                    Type = OperandType.Expression,
                    A = Build3(GETTABLE, 0, table, indexer),
                    B = Build3(SETTABLE, table, indexer, 0),
                });
            }
        }

        public void Local(int idx)
        {
            operands.Add(new Operand {
                Type = OperandType.Local,
                A = idx,
            });
        }

        public void Upvalue(int idx)
        {
            operands.Add(new Operand {
                Type = OperandType.Upvalue,
                A = idx,
            });
        }

        public void InitLocals(int count)
        {
            var op = Pop();
            switch (op.Type) {
                case OperandType.Vararg:
                    code.Add(Build2(VARARG, PushS(), Math.Max(1, count - arguments)));
                    break;
                default:
                    Argument();
                    while (arguments < count) {
                        Constant(LuaValue.Nil);
                        Argument();
                    }
                    break;
            }
            arguments = 0;
        }

        public void Jump(int label)
        {
            throw new System.NotImplementedException();
        }

        public void JumpIf(int label, bool condition)
        {
            throw new System.NotImplementedException();
        }

        public int Label()
        {
            throw new System.NotImplementedException();
        }

        public void Mark(int label)
        {
            throw new System.NotImplementedException();
        }

        public void Unary(TokenType token)
        {
            throw new System.NotImplementedException();
        }

        public void Vararg()
        {
            operands.Add(new Operand {
                Type = OperandType.Vararg,
            });
        }
    }
}