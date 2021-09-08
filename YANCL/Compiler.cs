using System;
using System.Collections.Generic;
using System.Diagnostics;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{

    public class Compiler
    {
        readonly Lexer lexer;
        readonly List<int> code = new List<int>(1024);
        readonly Compiler? parent;
        readonly List<LuaValue> constants = new List<LuaValue>();
        readonly List<string> locals = new List<string>();

        Compiler(string str) {
            lexer = new Lexer(str);
        }

        int Top;
        int Head => Top - 1;
        int nSlots;
        int nResults;
        int firstResult;
        int maxStack;
        int pcAtMaxStack;

        void SetTop(int top) {
            if (top > maxStack) {
                maxStack = top;
                pcAtMaxStack = code.Count;
            }
            Top = top;
        }

        int EnvUp() => 0;

        int Push() {
            Top++;
            if (Top > maxStack) {
                maxStack = Top;
                pcAtMaxStack = code.Count;
            }
            return Head;
        }

        int PopS() {
            return --Top;
        }

        int PopRK() {
            void Shrink() {
                code.RemoveAt(code.Count - 1);
                if (code.Count == pcAtMaxStack && maxStack == Top) {
                    maxStack--;
                    pcAtMaxStack--;
                }
                Top--;
            }

            Debug.Assert(Top > 0);
            Debug.Assert(code.Count > 0);
            var inst = code[code.Count - 1];
            switch (GetOpCode(inst)) {
                case MOVE:
                    if (GetA(inst) == Head) {
                        Shrink();
                        return GetB(inst);
                    }
                    break;
                case LOADK:
                    if (GetA(inst) == Head) {
                        Shrink();
                        return GetBx(inst) | KFlag;
                    }
                    break;
                case LOADBOOL:
                    if (GetA(inst) == Head) {
                        Shrink();
                        return Constant(GetB(inst) != 0);
                    }
                    break;
                case LOADNIL:
                    if (GetA(inst) == Head && GetB(inst) == 0) {
                        Shrink();
                        return Constant(LuaValue.Nil);
                    }
                    break;
                default:
                    break;
            }
            return PopS();
        }

        Token Next() => lexer.Next();
        TokenType Peek() => lexer.Peek();
        string? Expect(TokenType type, string after) => lexer.Expect(type, after);
        string ParseString(char term) => lexer.ParseString(term);


        public static LuaFunction Compile(string str) {
            var c = new Compiler(str);
            c.ParseChunk();
            return new LuaFunction {
                code = c.code.ToArray(),
                constants = c.constants.ToArray(),
                upvalues = Array.Empty<LuaUpValue>(),
                prototypes = Array.Empty<LuaFunction>(),
                nLocals = c.locals.Count,
                nSlots = c.maxStack - c.locals.Count,
            };
        }

        void ParseChunk() {
            while (true) {
                switch (Peek()) {
                    case TokenType.Eof:
                        return;
                    default:
                        ParseStat();
                        break;
                }
            }
        }

        void ParseStat() {
            nSlots = 0;
            nResults = 0;
            switch (Peek()) {
                case TokenType.Identifier:
                case TokenType.OpenParen:
                case TokenType.OpenBrace:
                case TokenType.Minus:
                    ParseVar(resultIdx: 0);
                    break;
                case TokenType.Do:
                    Next();
                    ParseBlock();
                    break;
                case TokenType.If:
                    throw new NotImplementedException();
                case TokenType.For:
                    throw new NotImplementedException();
                case TokenType.While:
                    throw new NotImplementedException();
                case TokenType.Local:
                    ParseLocal();
                    break;
                case TokenType.Function:
                    throw new NotImplementedException();
                case TokenType.Return:
                    throw new NotImplementedException();
                case TokenType.Semicolon:
                    Next();
                    break;
                default:
                    throw new Exception($"Unexpected token {Peek()}");
            }
        }

        int Constant(LuaValue value) {
            var constIdx = constants.IndexOf(value);
            if (constIdx < 0) {
                constIdx = constants.Count;
                constants.Add(value);
            }
            return constIdx | KFlag;
        }

        void PushK(LuaValue value) {
            code.Add(Build2x(LOADK, Push(), Constant(value) & ~KFlag));
        }

        bool PeekSuffix() {
            switch (Peek()) {
                case TokenType.Dot:
                case TokenType.OpenBracket:
                case TokenType.OpenParen:
                case TokenType.SingleQuote:
                case TokenType.DoubleQuote:
                case TokenType.OpenBrace:
                    return true;
                default:
                    return false;
            }
        }

        bool PeekAssignment() {
            if (Peek() == TokenType.Equal) {
                Next();
                return true;
            }
            return false;
        }

        bool PeekComma() {
            if (Peek() == TokenType.Comma) {
                Next();
                return true;
            }
            return false;
        }

        int ParseArgs() {
            var nArgs = 0;
            while (Peek() != TokenType.CloseParen) {
                nArgs++;
                PushExpression();
                if (Peek() == TokenType.Comma) {
                    Next();
                } else if (Peek() != TokenType.CloseParen) {
                    throw new Exception($"Expected ',' or ')' but got {Peek()}");
                }
            }
            Next();
            if (ExtendVararg(0)) {
                nArgs = -1;
            }
            return nArgs;
        }

        bool ExtendVararg(int nReturns) {
            var inst = code[code.Count - 1];
            switch (Instruction.GetOpCode(inst)) {
                case CALL: {
                    var a = Instruction.GetA(inst);
                    var b = Instruction.GetB(inst);
                    code[code.Count - 1] = Build3(CALL, a, b, nReturns);
                    return true;
                }
                case VARARG: {
                    var a = Instruction.GetA(inst);
                    code[code.Count - 1] = Build2(VARARG, a, nReturns);
                    return true;
                }
            }
            return false;
        }

        int ParseAssignment(bool forceStack = false) {
            firstResult = Top;
            nResults = 0;
            while (true) {
                nResults++;
                PushExpression();
                if (Peek() == TokenType.Comma) {
                    Next();
                } else {
                    break;
                }
            }
            int result;
            if (nResults != nSlots) {
                if (nResults < nSlots) {
                    if (ExtendVararg(nSlots - nResults + 2)) {
                        nResults = nSlots;
                    }
                }
                if (nResults < nSlots) {
                    code.Add(Build2(LOADNIL, firstResult + nResults, firstResult + nSlots - nResults - 1));
                }
                nResults = nSlots;
                SetTop(firstResult + nSlots);
                // PopRK() would be valid here, but the reference implementation doesn't do it.
                result = PopS();
            } else {
                result = forceStack ? PopS() : PopRK();
            }
            SetTop(firstResult);
            return result;
        }

        void ParseVarSuffix(int resultIdx) {

            void PostCall(int func, int nArgs) {
                if (PeekSuffix()) {
                    code.Add(Build3(CALL, func, nArgs + 1, 2));
                    SetTop(func + 1);
                    ParseVarSuffix(resultIdx);
                } else if (PeekAssignment() || PeekComma() || nSlots > 1) {
                    throw new Exception("Cannot assign to function call");
                } else {
                    code.Add(Build3(CALL, func, nArgs + 1, 1));
                    SetTop(func);
                }
            }

            var token = Next();
            switch (token.type) {
                case TokenType.Dot: {
                    var name = Expect(TokenType.Identifier, "dot")!;
                    var constIdx = Constant(name);
                    if (PeekSuffix()) {
                        code.Add(Build3(GETTABLE, Head, Head, constIdx));
                        ParseVarSuffix(resultIdx);
                    } else if (PeekAssignment()) {
                        var result = ParseAssignment();
                        code.Add(Build3(SETTABLE, PopS(), constIdx, result));
                    } else if (PeekComma()) {
                        ParseVarAdditional(resultIdx);
                        code.Add(Build3(SETTABLE, PopS(), constIdx, firstResult + resultIdx));
                    } else {
                        throw new Exception($"Unexpected token {token.type}");
                    }
                    break;
                }
                case TokenType.OpenBracket: {
                    PushExpression();
                    Expect(TokenType.CloseBracket, "index expression");
                    if (PeekSuffix()) {
                        var indexer = PopRK();
                        code.Add(Build3(GETTABLE, Head, Head, indexer));
                        ParseVarSuffix(resultIdx);
                    } else if (PeekAssignment()) {
                        var result = ParseAssignment();
                        var indexer = PopRK();
                        code.Add(Build3(SETTABLE, PopS(), indexer, result));
                    } else if (PeekComma()) {
                        ParseVarAdditional(resultIdx);
                        var indexer = PopRK();
                        code.Add(Build3(SETTABLE, PopS(), indexer, firstResult + resultIdx));
                    } else {
                        throw new Exception($"Unexpected token {token.type}");
                    }
                    break;
                }
                case TokenType.OpenParen: {
                    var func = Head;
                    var nArgs = ParseArgs();
                    PostCall(func, nArgs);
                    break;
                }
                case TokenType.SingleQuote:
                case TokenType.DoubleQuote: {
                    var func = Head;
                    PushK(ParseString(token.type == TokenType.SingleQuote ? '\'' : '"'));
                    PostCall(func, 1);
                    break;
                }
                case TokenType.OpenBrace: {
                    var func = Head;
                    PushTableConstructor();
                    PostCall(func, 1);
                    break;
                }
                default:
                    Debug.Assert(false);
                    break;
            }
        }

        void PushExpressionSuffix() {

            void PostCall(int func, int nArgs) {
                if (PeekSuffix()) {
                    code.Add(Build3(CALL, func, nArgs + 1, 2));
                    SetTop(func + 2);
                    PushExpressionSuffix();
                } else {
                    code.Add(Build3(CALL, func, nArgs + 1, 2));
                    SetTop(func + 1);
                }
            }

            switch (Peek()) {
                case TokenType.Dot: {
                    Next();
                    var name = Expect(TokenType.Identifier, "dot")!;
                    var indexer = Constant(name);
                    code.Add(Build3(GETTABLE, Head, Head, indexer));
                    PushExpressionSuffix();
                    break;
                }
                case TokenType.OpenBracket: {
                    Next();
                    PushExpression();
                    Expect(TokenType.CloseBracket, "index expression");
                    var indexer = PopRK();
                    code.Add(Build3(GETTABLE, Head, Head, indexer));
                    PushExpressionSuffix();
                    break;
                }
                case TokenType.OpenParen: {
                    Next();
                    var func = Head;
                    var nArgs = ParseArgs();
                    PostCall(func, nArgs);
                    break;
                }
                case TokenType.SingleQuote:
                case TokenType.DoubleQuote: {
                    Next();
                    var func = Head;
                    var arg = Constant(ParseString(Peek() == TokenType.SingleQuote ? '\'' : '"'));
                    SetTop(func + 2);
                    PostCall(func, 1);
                    break;
                }
                case TokenType.OpenBrace: {
                    var func = Head;
                    PushTableConstructor();
                    SetTop(func + 2);
                    PostCall(func, 1);
                    break;
                }
                default:
                    return;
            }
        }

        void PushExpression() {
            PushTerm();
        }

        void PushTerm() {
            void Unary(OpCode op) {
                PushTerm();
                var expr = PopS();
                code.Add(Build2(op, Push(), expr));
            }

            void Literal(OpCode op, int arg) {
                code.Add(Build2(op, Push(), arg));
            }

            var token = Next();
            switch (token.type) {
                case TokenType.Identifier: {
                    var name = token.text!;
                    var localIdx = locals.IndexOf(name);
                    if (localIdx >= 0) {
                        code.Add(Build2(MOVE, Push(), localIdx));
                    } else {
                        var globalIdx = Constant(name);
                        code.Add(Build3(GETTABUP, Push(), EnvUp(), globalIdx));
                    }
                    PushExpressionSuffix();
                    break;
                }
                case TokenType.Number: PushK(token.number); break;
                case TokenType.DoubleQuote: PushK(ParseString('"')); break;
                case TokenType.SingleQuote: PushK(ParseString('\'')); break;
                case TokenType.OpenParen: {
                    PushExpression();
                    Expect(TokenType.CloseParen, "parenthesized expression");
                    PushExpressionSuffix();
                    break;
                }
                case TokenType.Hash: Unary(LEN); break;
                case TokenType.Not: Unary(NOT); break;
                case TokenType.Minus: Unary(UNM); break;
                case TokenType.True: Literal(LOADBOOL, 1); break;
                case TokenType.False: Literal(LOADBOOL, 0); break;
                case TokenType.Nil: Literal(LOADNIL, 0); break;
                case TokenType.OpenBrace: PushTableConstructor(); break;
                case TokenType.TripleDot:
                    code.Add(Build2(VARARG, Push(), 2));
                    break;
                default:
                    throw new Exception($"Unexpected token {token.type}");
            }
        }

        void PushTableConstructor() {
            var opIdx = code.Count;
            var table = Push();
            code.Add(0);
            int nArr = 0;
            int nHash = 0;
            while (Peek() != TokenType.CloseBrace) {
                switch (Peek()) {
                    case TokenType.Identifier: {
                        var key = Next();
                        if (Peek() == TokenType.Equal) {
                            Next();
                            var constIdx = Constant(key.text!);
                            PushExpression();
                            code.Add(Build3(SETTABLE, table, constIdx, PopRK()));
                            nHash++;
                        } else {
                            lexer.PushBack(key);
                            PushExpression();
                            nArr++;
                        }
                        break;
                    }
                    case TokenType.OpenBracket: {
                        Next();
                        PushExpression();
                        Expect(TokenType.CloseBracket, "table index expression");
                        Expect(TokenType.Equal, "table index");
                        PushExpression();
                        var value = PopRK();
                        code.Add(Build3(SETTABLE, table, PopRK(), value));
                        nHash++;
                        break;
                    }
                    default:
                        PushExpression();
                        nArr++;
                        break;
                }
                switch (Peek()) {
                    case TokenType.Comma:
                        Next();
                        break;
                    case TokenType.CloseBrace:
                        break;
                    default:
                        throw new Exception("'}' or ',' expected");
                }
            }
            Next();
            if (nArr > 0) {
                if (ExtendVararg(0)) {
                    nArr = 0;
                }
                code.Add(Build3(SETLIST, table, nArr, 1));
            }
            code[opIdx] = Build3(NEWTABLE, table, nArr, nHash);
            SetTop(table + 1);
        }

        void ParseVarAdditional(int resultIdx) {
            ParseVar(resultIdx + 1);
        }

        void ParseVar(int resultIdx) {
            nSlots++;
            var token = Next();
            switch (token.type) {
                case TokenType.Identifier: {
                    var name = token.text!;
                    var localIdx = locals.IndexOf(name);
                    if (localIdx < 0) {
                        var constIdx = Constant(name);
                        if (PeekSuffix()) {
                            code.Add(Build3(GETTABUP, Push(), EnvUp(), constIdx));
                            ParseVarSuffix(resultIdx);
                        } else if (PeekAssignment()) {
                            var result = ParseAssignment();
                            code.Add(Build3(SETTABUP, EnvUp(), constIdx, result));
                        } else if (PeekComma()) {
                            ParseVarAdditional(resultIdx);
                            code.Add(Build3(SETTABUP, EnvUp(), constIdx, firstResult + resultIdx));
                        } else {
                            throw new Exception("Expected a call or assignment");
                        }
                    } else {
                        if (PeekSuffix()) {
                            code.Add(Build2(MOVE, Push(), localIdx));
                        } else if (PeekAssignment()) {
                            var result = ParseAssignment();
                            if ((result & KFlag) != 0) {
                                code.Add(Build2x(LOADK, localIdx, result & ~KFlag));
                            } else if (result != localIdx) {
                                code.Add(Build2(MOVE, localIdx, result));
                            }
                        } else if (PeekComma()) {
                            ParseVarAdditional(resultIdx);
                            code.Add(Build2(MOVE, localIdx, firstResult + resultIdx));
                        } else {
                            throw new Exception("Expected a call or assignment");
                        }
                    }
                    break;
                }
                case TokenType.OpenParen: {
                    PushExpression();
                    Expect(TokenType.CloseParen, "parenthesized expression");
                    if (PeekSuffix()) {
                        ParseVarSuffix(resultIdx);
                    } else if (PeekAssignment() || PeekComma()) {
                        throw new Exception("Cannot assign to parenthesized expression");
                    } else {
                        throw new Exception("Expected an call or assignment");
                    }
                    break;
                }
                default:
                    throw new Exception($"Unexpected token {token.type}");
            }
        }

        void ParseBlock() {
            while (true) {
                switch (Peek()) {
                    case TokenType.End:
                        Next();
                        return;
                    default:
                        ParseStat();
                        break;
                }
            }
        }

        private readonly List<string> tmpLocals = new List<string>();

        void ParseLocal() {
            do {
                Next();
                tmpLocals.Add(Expect(TokenType.Identifier, "local declaration")!);
            } while (Peek() == TokenType.Comma);
            nSlots = tmpLocals.Count;
            if (Peek() == TokenType.Equal) {
                Next();
                ParseAssignment(forceStack: true);
            } else {
                code.Add(Build2(LOADNIL, Top, Top + tmpLocals.Count - 1));
            }
            SetTop(firstResult + tmpLocals.Count);
            for (var i = 0; i < tmpLocals.Count; i++) {
                locals.Add(tmpLocals[i]);
            }
            tmpLocals.Clear();
        }
    }
}
