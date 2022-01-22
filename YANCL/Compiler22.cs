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
            public int A, B, Slots;
            public LuaValue Value;

            public override string ToString()
            {
                return Type switch {
                    OperandType.Nil => "nil",
                    OperandType.Local => $"local {A}",
                    OperandType.Constant => $"constant {Value}",
                    OperandType.Upvalue => $"upvalue {A}",
                    OperandType.Call => $"call {A}",
                    OperandType.Vararg => "vararg",
                    OperandType.Expression => $"expression {Stringify(A)}",
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
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
                    if (op.Value.Type == LuaType.BOOLEAN) {
                        return Build3(LOADBOOL, dst, op.Value.Boolean ? 1 : 0, 0);
                    } else {
                        return Build2x(LOADK, dst, K(op.Value));
                    }
                case OperandType.Expression:
                    return Build2x(GetOpCode(op.A), dst, GetBx(op.A));
                case OperandType.Vararg:
                    return Build2(VARARG, dst, 2);
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
            if (arguments + 2 == operands.Count) {
                var target = Peek(1);
                switch (target.Type) {
                    case OperandType.Local:
                        code.Add(LoadInst(Pop(), target.A));
                        break;
                    default:
                        var slots = 0;
                        code.Add(StoreInst(target, PopRK(ref slots)));
                        top -= slots;
                        break;
                }
                Pop();
            } else {
                Argument();
                while (arguments < operands.Count) {
                    Constant(LuaValue.Nil);
                    Argument();
                }
            }
            while (arguments > operands.Count) {
                arguments--;
                top--;
            }
            while (operands.Count > 0) {
                code.Add(StoreInst(Pop(), --top));
            }
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
                var slots = 0;
                var b = PopRK(ref slots);
                var a = PopRK(ref slots);
                operands.Add(new Operand {
                    Type = OperandType.Expression,
                    A = Build3(inst, 0, a, b),
                    B = -1,
                    Slots = slots,
                });
            }
        }

        public void Call()
        {
            var slots = 0;
            operands.Add(new Operand {
                Type = OperandType.Call,
                A = PopRK(ref slots),
                B = arguments + 1,
                Slots = slots,
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

        private int PopRK(ref int slots)
        {
            var op = Pop();
            switch (op.Type) {
                case OperandType.Nil:
                    return K(LuaValue.Nil) | KFlag;
                case OperandType.Local:
                    slots += op.Slots;
                    top += op.Slots;
                    maxStack = Math.Max(maxStack, top);
                    return op.A;
                case OperandType.Constant:
                    return K(op.Value) | KFlag;
                default:
                    slots++;
                    var slot = PushS();
                    code.Add(LoadInst(op, slot));
                    return slot;
            }
        }

        Operand Pop() {
            var op = operands[operands.Count - 1];
            top -= op.Slots;
            operands.RemoveAt(operands.Count - 1);
            return op;
        }

        Operand Peek(int index) {
            return operands[operands.Count - index - 1];
        }

        private void EmitOperand(int idx, bool keepUpvalues) {
            var op = Peek(idx);
            if (op.Type == OperandType.Expression || op.Type == OperandType.Vararg || (!keepUpvalues && op.Type == OperandType.Upvalue)) {
                top -= op.Slots;
                var slot = PushS();
                code.Add(LoadInst(op, slot));
                operands[operands.Count - 1 - idx] = new Operand {
                    Type = OperandType.Local,
                    A = slot,
                    Slots = 1
                };
            }
        }

        public void Callee() {
            EmitOperand(0, keepUpvalues: false);
        }

        // public void Indexee() {
        //     // EmitOperand(0, keepUpvalues: true);
        // }

        public void Index()
        {
            EmitOperand(1, keepUpvalues: true);
            var slots = 0;
            var indexer = PopRK(ref slots);
            if (Peek(0).Type == OperandType.Upvalue) {
                var upval = Pop().A;
                operands.Add(new Operand {
                    Type = OperandType.Expression,
                    A = Build3(GETTABUP, 0, upval, indexer),
                    B = Build3(SETTABUP, upval, indexer, 0),
                    Slots = slots,
                });
            } else {
                var table = PopRK(ref slots);
                operands.Add(new Operand {
                    Type = OperandType.Expression,
                    A = Build3(GETTABLE, 0, table, indexer),
                    B = Build3(SETTABLE, table, indexer, 0),
                    Slots = slots,
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

        public void Return()
        {
            var op = Pop();
            switch (op.Type) {
                case OperandType.Vararg:
                    code.Add(Build2(VARARG, PushS(), 0));
                    break;
                default:
                    Argument();
                    break;
            }
            arguments = 0;
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
            if (operands.Count == 0) {
                Constant(LuaValue.Nil);
            }
            switch (Peek(0).Type) {
                case OperandType.Vararg:
                    Pop();
                    code.Add(Build2(VARARG, PushS(), Math.Max(0, count - arguments) + 1));
                    arguments++;
                    while (arguments < count) {
                        arguments++;
                        PushS();
                    }
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

        public void NewTable()
        {
            operands.Add(new Operand {
                Type = OperandType.Expression,
                A = Build3(NEWTABLE, 0, 0, 0),
                B = -1
            });
        }

        public void Closure(LuaFunction function)
        {
            throw new NotImplementedException();
        }

        public void SetList()
        {
            throw new NotImplementedException();
        }

        public LuaFunction MakeFunction()
        {
            return new LuaFunction {
                code = code.ToArray(),
                constants = constants.ToArray(),
                upvalues = Array.Empty<LuaUpValue>(),
                entry = 0,
                nParams = 0,
                nLocals = top,
                nSlots = maxStack - top,
            };
        }
    }
}