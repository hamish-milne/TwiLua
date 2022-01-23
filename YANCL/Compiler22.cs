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
            public int A, B, ArgsOnStack;
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
                case OperandType.Call:
                    return Build3(CALL, op.A, op.B, 2);
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
        // private readonly Stack<int> callees = new Stack<int>();

        private int top;
        // private int arguments;
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
            // arguments++;
        }

        public void Assign(int arguments)
        {
            if (arguments + 1 == operands.Count) {
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
                    arguments++;
                    Constant(LuaValue.Nil);
                    Argument();
                }
                while (arguments > operands.Count) {
                    arguments--;
                    top--;
                }
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
                    ArgsOnStack = slots,
                });
            }
        }

        public void Call(int arguments)
        {
            bool hasDispatch = arguments > 0 && PushVarargs(0);
            operands.Add(new Operand {
                Type = OperandType.Call,
                A = top-arguments-1,
                B = hasDispatch ? 0 : arguments+1,
                ArgsOnStack = arguments + 1,
            });
        }

        public void Discard()
        {
            if (Peek(0).Type != OperandType.Call) {
                throw new InvalidOperationException();
            }
            var op = Pop();
            code.Add(Build3(CALL, op.A, op.B, 1));
        }

        private int PopRK(ref int argsOnStack)
        {
            var op = Pop();
            switch (op.Type) {
                case OperandType.Nil:
                    return K(LuaValue.Nil) | KFlag;
                case OperandType.Local:
                    argsOnStack += op.ArgsOnStack;
                    top += op.ArgsOnStack;
                    maxStack = Math.Max(maxStack, top);
                    return op.A;
                case OperandType.Constant:
                    return K(op.Value) | KFlag;
                default:
                    argsOnStack++;
                    var slot = PushS();
                    code.Add(LoadInst(op, slot));
                    return slot;
            }
        }

        Operand Pop() {
            var op = operands[operands.Count - 1];
            top -= op.ArgsOnStack;
            operands.RemoveAt(operands.Count - 1);
            return op;
        }

        Operand Peek(int index) {
            return operands[operands.Count - index - 1];
        }

        private void EmitOperand(int idx, bool keepUpvalues) {
            var op = Peek(idx);
            if (op.Type == OperandType.Expression || op.Type == OperandType.Vararg || (!keepUpvalues && op.Type == OperandType.Upvalue)) {
                top -= op.ArgsOnStack;
                var slot = PushS();
                code.Add(LoadInst(op, slot));
                operands[operands.Count - 1 - idx] = new Operand {
                    Type = OperandType.Local,
                    A = slot,
                    ArgsOnStack = 1
                };
            }
        }

        public void Callee() {
            Argument();
            // EmitOperand(0, keepUpvalues: false);
            // callees.Push(operands.Count);
        }

        // public void Indexee() {
        //     // EmitOperand(0, keepUpvalues: true);
        // }

        public void Index()
        {
            EmitOperand(1, keepUpvalues: true);
            var argsOnStack = 0;
            var indexer = PopRK(ref argsOnStack);
            int table;
            OpCode getter, setter;
            if (Peek(0).Type == OperandType.Upvalue) {
                table = Pop().A;
                getter = GETTABUP;
                setter = SETTABUP;
            } else {
                table = PopRK(ref argsOnStack);
                getter = GETTABLE;
                setter = SETTABLE;
            }
            operands.Add(new Operand {
                Type = OperandType.Expression,
                A = Build3(getter, 0, table, indexer),
                B = Build3(setter, table, indexer, 0),
                ArgsOnStack = argsOnStack,
            });
        }

        public void Local(int idx)
        {
            operands.Add(new Operand {
                Type = OperandType.Local,
                A = idx,
            });
        }

        private bool PushVarargs(int limit)
        {
            var op = Peek(0);
            switch (op.Type) {
                case OperandType.Vararg:
                    Pop();
                    code.Add(Build2(VARARG, PushS(), limit));
                    return true;
                case OperandType.Call:
                    Pop();
                    code.Add(Build3(CALL, op.A, op.B, limit));
                    PushS();
                    return true;
                default:
                    Argument();
                    return false;
            }
        }

        public void Return(int arguments)
        {
            if (PushVarargs(0)) {
                code.Add(Build2(RETURN, top-arguments-1, 0));
            } else {
                code.Add(Build2(RETURN, top-arguments-1, arguments));
            }
        }

        public void Upvalue(int idx)
        {
            operands.Add(new Operand {
                Type = OperandType.Upvalue,
                A = idx,
            });
        }

        public void InitLocals(int count, int arguments)
        {
            if (arguments > 0 && PushVarargs(Math.Max(0, count - arguments) + 1)) {
                arguments++;
                while (arguments < count) {
                    arguments++;
                    PushS();
                }
            } else {
                while (arguments < count) {
                    arguments++;
                    Constant(LuaValue.Nil);
                    Argument();
                }
            }
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