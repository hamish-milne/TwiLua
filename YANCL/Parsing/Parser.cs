using System;
using System.Collections.Generic;

namespace YANCL
{
    internal sealed class Parser
    {
        readonly Lexer lexer;
        readonly Compiler C;
        readonly Stack<Label> breakLabels = new Stack<Label>();

        Token Next() {
            var token = lexer.Next();
            C.Location = token.location;
            return token;
        }
        Token Peek() => lexer.Peek();
        string? Expect(TokenType type, string after) => lexer.Expect(type, after);
        string ParseString(char term) {
            var (value, location) = lexer.ParseString(term);
            C.Location = location;
            return value;
        }
        bool TryTake(TokenType type) {
            if (Peek().type == type) {
                Next();
                return true;
            }
            return false;
        }


        public Parser(Lexer lexer, Compiler compiler) {
            this.lexer = lexer;
            C = compiler;
        }

        bool PeekSuffix() {
            switch (Peek().type) {
                case TokenType.Dot:
                case TokenType.OpenBracket:
                case TokenType.OpenParen:
                case TokenType.SingleQuote:
                case TokenType.DoubleQuote:
                case TokenType.OpenBrace:
                case TokenType.Colon:
                    return true;
                default:
                    return false;
            }
        }

        public LuaFunction ParseChunk() {
            C.IsVaradic = true;
            C.PushScope();
            while (!TryTake(TokenType.Eof)) {
                ParseStat();
                C.AssertStatementEnd();
            }
            C.PopScope();
            C.Return(0);
            return C.MakeFunction();
        }

        void ParseBlock() {
            while (!TryTake(TokenType.End)) {
                ParseStat();
            }
        }

        void PushBreak() => PushBreak(C.Label());
        void PushBreak(Label label) => breakLabels.Push(label);
        void PopBreak() => C.Mark(breakLabels.Pop());

        void ParseStat() {
            switch (Peek().type) {
                case TokenType.Identifier:
                case TokenType.OpenParen:
                    ParseVar(1);
                    break;
                case TokenType.Local:
                    ParseLocal();
                    break;
                case TokenType.Semicolon:
                    Next();
                    break;
                case TokenType.Function:
                    Next();
                    C.Identifier(Expect(TokenType.Identifier, "function name")!);
                    while (TryTake(TokenType.Dot)) {
                        C.Indexee();
                        C.Constant(Expect(TokenType.Identifier, "function declaration member access")!);
                        C.Index();
                    }
                    bool hasSelf = false;
                    if (TryTake(TokenType.Colon)) {
                        C.Indexee();
                        C.Constant(Expect(TokenType.Identifier, "method declaration")!);
                        C.Index();
                        hasSelf = true;
                    }
                    ParseFunction(hasSelf, C.Closure());
                    C.Assign(1, 1);
                    break;
                case TokenType.Return: {
                    Next();
                    int argc = 0;
                    switch (Peek().type) {
                        case TokenType.Eof:
                        case TokenType.Semicolon:
                        case TokenType.End:
                            break;
                        default:
                            argc = ParseArgumentList(0);
                            break;
                    }
                    C.Return(argc);
                    while (Peek().type == TokenType.Semicolon) {
                        Next();
                    }
                    switch (Peek().type) {
                        case TokenType.End:
                        case TokenType.Eof:
                        case TokenType.Else:
                        case TokenType.ElseIf:
                            break;
                        default:
                            throw new Exception($"Unexpected {Peek()} after return");
                    }
                    break;
                }
                case TokenType.If:
                    ParseIfBody();
                    break;
                case TokenType.While: {
                    Next();
                    var c0 = C.Label();
                    C.Mark(c0);
                    ParseExpression(condition: true);
                    Expect(TokenType.Do, "condition");
                    var c1 = C.Label();
                    PushBreak(c1);
                    C.JumpIf(c1, false);
                    C.PushScope();
                    ParseBlock();
                    C.PopScope();
                    C.Jump(c0);
                    PopBreak();
                    break;
                }
                case TokenType.Repeat: {
                    Next();
                    var c0 = C.Label();
                    PushBreak();
                    C.Mark(c0);
                    C.PushScope();
                    while (!TryTake(TokenType.Until)) {
                        ParseStat();
                    }
                    ParseExpression(condition: true);
                    C.PopScope();
                    C.JumpIf(c0, false);
                    PopBreak();
                    break;
                }
                case TokenType.For: {
                    Next();
                    C.PushScope();
                    do {
                        tmpLocals.Add(Expect(TokenType.Identifier, "for loop variable name")!);
                    } while (TryTake(TokenType.Comma));
                    if (tmpLocals.Count == 1 && TryTake(TokenType.Equal)) {
                        ParseExpression();
                        C.Argument();
                        Expect(TokenType.Comma, "for loop separator");
                        ParseExpression();
                        C.Argument();
                        if (TryTake(TokenType.Comma)) {
                            ParseExpression();
                        } else {
                            C.Constant(1);
                        }
                        Expect(TokenType.Do, "for loop body");
                        C.ForInit();
                        var state = C.ForPrep();
                        PushBreak();
                        C.PushScope();
                        C.Reserve(tmpLocals[0]);
                        tmpLocals.Clear();
                        ParseBlock();
                        C.PopScope();
                        C.ForLoop(state);
                        PopBreak();
                    } else {
                        Expect(TokenType.In, "generic for loop");
                        var argc = ParseArgumentList(0);
                        Expect(TokenType.Do, "for loop body");
                        var state = C.GForInit(argc);
                        PushBreak();
                        C.PushScope();
                        foreach (var l in tmpLocals) {
                            C.Reserve(l);
                        }
                        var localCount = tmpLocals.Count;
                        tmpLocals.Clear();
                        ParseBlock();
                        C.PopScope();
                        C.GForLoop(state, localCount);
                        PopBreak();
                    }
                    C.PopScope();
                    break;
                }
                case TokenType.Do:
                    Next();
                    C.PushScope();
                    ParseBlock();
                    C.PopScope();
                    break;
                case TokenType.Break:
                    Next();
                    if (breakLabels.Count == 0) {
                        throw new Exception("Break outside of loop");
                    }
                    C.Jump(breakLabels.Peek());
                    break;
                default:
                    throw new Exception($"Unexpected token {Peek()}");
            }
        }

        void ParseIfBody() {
            Next();
            ParseExpression(condition: true);
            Expect(TokenType.Then, "condition");
            var c1 = C.Label();
            if (TryTake(TokenType.Break)) {
                if (breakLabels.Count == 0) {
                    throw new Exception("Break outside of loop");
                }
                C.JumpIf(breakLabels.Peek(), true);
                C.Jump(c1);
            } else {
                C.JumpIf(c1, false);
            }
            C.PushScope();
            while (true) {
                switch (Peek().type) {
                    case TokenType.End:
                        C.PopScope();
                        C.Mark(c1);
                        Next();
                        return;
                    case TokenType.Else: {
                        C.PopScope();
                        Next();
                        var c2 = C.Label();
                        C.Jump(c2);
                        C.Mark(c1);
                        ParseBlock();
                        C.Mark(c2);
                        return;
                    }
                    case TokenType.ElseIf: {
                        C.PopScope();
                        var c2 = C.Label();
                        C.Jump(c2);
                        C.Mark(c1);
                        ParseIfBody();
                        C.Mark(c2);
                        return;
                    }
                }
                ParseStat();
            }
        }

        void ParseVar(int nTargets) {
            var token = Next();
            switch (token.type) {
                case TokenType.Identifier: {
                    C.Identifier(token.text!);
                    ContinueParseVar(nTargets);
                    break;
                }
                case TokenType.OpenParen:
                    ParseExpression();
                    Expect(TokenType.CloseParen, "parenthesized expression");
                    if (!PeekSuffix()) {
                        throw new Exception("Expected a token after expression");
                    }
                    ContinueParseVar(nTargets);
                    break;
            }
        }

        void ContinueParseVar(int nTargets) {
            var endsInCall = false;
            ParseSuffix(ref endsInCall);
            if (endsInCall) {
                if (nTargets == 1) {
                    // Call statement
                    C.Discard();
                    return;
                } else {
                    throw new Exception("Cannot assign to a function call");
                }
            }
            var token = Next();
            switch (token.type) {
                case TokenType.Equal:
                    int argc = ParseArgumentList(0);
                    C.Assign(argc, nTargets);
                    break;
                case TokenType.Comma:
                    ParseVar(nTargets + 1);
                    break;
                default:
                    throw new Exception($"Unexpected token {token}");
            }
        }

        void ParseSuffix() {
            var endsInCall = false;
            ParseSuffix(ref endsInCall);
        }

        void ParseSuffix(ref bool endsInCall) {
            switch (Peek().type) {
                case TokenType.Dot:
                    C.Indexee();
                    Next();
                    var name = Expect(TokenType.Identifier, "dot")!;
                    C.Constant(name);
                    C.Index();
                    endsInCall = false;
                    ParseSuffix(ref endsInCall);
                    break;
                case TokenType.OpenBracket:
                    C.Indexee();
                    Next();
                    ParseExpression();
                    Expect(TokenType.CloseBracket, "index expression");
                    C.Index();
                    endsInCall = false;
                    ParseSuffix(ref endsInCall);
                    break;
                case TokenType.OpenParen:
                    C.Callee();
                    Next();
                    ParseCall(0);
                    endsInCall = true;
                    ParseSuffix(ref endsInCall);
                    break;
                case TokenType.Colon:
                    C.Indexee();
                    Next();
                    C.Constant(Expect(TokenType.Identifier, "self call operator")!);
                    C.Self();
                    Expect(TokenType.OpenParen, "self call operator");
                    ParseCall(1);
                    endsInCall = true;
                    ParseSuffix(ref endsInCall);
                    break;
                case TokenType.DoubleQuote:
                case TokenType.SingleQuote:
                case TokenType.OpenBrace:
                    C.Callee();
                    ParseTerm();
                    C.Call(1);
                    endsInCall = true;
                    break;
                default:
                    break;
            }
        }

        void ParseCall(int argc) {
            if (!TryTake(TokenType.CloseParen)) {
                argc = ParseArgumentList(argc);
                Expect(TokenType.CloseParen, "argument list");
            }
            C.Call(argc);
        }

        void ParseTerm() {
            var token = Next();
            switch (token.type) {
                case TokenType.Identifier:
                    C.Identifier(token.text!);
                    ParseSuffix();
                    break;
                case TokenType.Number: C.Constant(token.number); break;
                case TokenType.DoubleQuote: C.Constant(ParseString('"')); break;
                case TokenType.SingleQuote: C.Constant(ParseString('\'')); break;
                case TokenType.True: C.Constant(true); break;
                case TokenType.False: C.Constant(false); break;
                case TokenType.Nil: C.Constant(LuaValue.Nil); break;
                case TokenType.OpenBrace: ParseTableConstructor(); break;
                case TokenType.TripleDot: C.Vararg(); break;
                case TokenType.Function: ParseFunction(hasSelf: false, C.Closure()); break;
                default: throw new Exception($"Unexpected token {token.type}");
            }
        }

        struct Operation {
            public bool isUnary;
            public int order;
            public Token token;
            public bool? isLogical;
        }

        readonly Stack<Operation> operations = new Stack<Operation>();
        private int parens;

        Token? GetUOP(out int order) {
            order = 10;
            var token = Peek();
            switch (token.type) {
                case TokenType.Not:
                case TokenType.Hash:
                case TokenType.Minus:
                case TokenType.Tilde:
                    Next();
                    return token;
                default:
                    return null;
            }
        }

        Token? GetBOP(out int order, out bool isLogical) {
            order = -1;
            var token = Peek();
            order = token.type switch {
                TokenType.Or => 0,
                TokenType.And => 1,
                TokenType.LessThan => 2,
                TokenType.GreaterThan => 2,
                TokenType.LessThanEqual => 2,
                TokenType.GreaterThanEqual => 2,
                TokenType.DoubleEqual => 2,
                TokenType.NotEqual => 2,
                TokenType.Pipe => 3,
                TokenType.Tilde => 4,
                TokenType.Ampersand => 5,
                TokenType.ShiftLeft => 6,
                TokenType.ShiftRight => 6,
                TokenType.DoubleDot => 7,
                TokenType.Plus => 8,
                TokenType.Minus => 8,
                TokenType.Star => 9,
                TokenType.Slash => 9,
                TokenType.DoubleSlash => 9,
                TokenType.Percent => 9,
                TokenType.Caret => 11,
                _ => -1
            };
            isLogical = order >= 0 && order <= 1;
            if (order >= 0) {
                Next();
                return token;
            }
            return null;
        }

        void ResolveOperations(int order, bool condition) {
            while (operations.Count > 0 && operations.Peek().order >= order) {
                var op = operations.Pop();
                if (op.token.type == TokenType.CloseParen) {
                    ResolveOperations(-1, condition);
                    if (operations.Pop().token.type != TokenType.OpenParen) {
                        throw new InvalidOperationException();
                    }
                    continue;
                }
                if (op.token.type == TokenType.OpenParen) {
                    operations.Push(op);
                    break;
                }
                C.Location = op.token.location;
                if (condition && op.token.type == TokenType.Not) {
                    C.Test();
                }
                condition = op.isLogical ?? condition;
                switch (op.token.type) {
                    case TokenType.Not: C.Not(); break;
                    case TokenType.Hash: C.Len(); break;
                    case TokenType.Minus:
                        if (op.isUnary) {
                            C.Unm();
                        } else {
                            C.Sub();
                        }
                        break;
                    case TokenType.Or: C.Or(); break;
                    case TokenType.And: C.And(); break;
                    case TokenType.LessThan: C.Lt(); break;
                    case TokenType.GreaterThan: C.Gt(); break;
                    case TokenType.LessThanEqual: C.Le(); break;
                    case TokenType.GreaterThanEqual: C.Ge(); break;
                    case TokenType.DoubleEqual: C.Eq(); break;
                    case TokenType.NotEqual: C.Ne(); break;
                    case TokenType.Pipe: C.BOr(); break;
                    case TokenType.Tilde:
                        if (op.isUnary) {
                            C.BNot();
                        } else {
                            C.BXor();
                        }
                        break;
                    case TokenType.Ampersand: C.BAnd(); break;
                    case TokenType.ShiftLeft: C.Shl(); break;
                    case TokenType.ShiftRight: C.Shr(); break;
                    case TokenType.DoubleDot: C.Concat(); break;
                    case TokenType.Plus: C.Add(); break;
                    case TokenType.Star: C.Mul(); break;
                    case TokenType.Slash: C.Div(); break;
                    case TokenType.DoubleSlash: C.IDiv(); break;
                    case TokenType.Percent: C.Mod(); break;
                    case TokenType.Caret: C.Pow(); break;
                    default: throw new InvalidOperationException();
                }
            }
        }

        void ParseExpression(bool condition = false) {
            while (true) {
                var uop = GetUOP(out var order);
                if (uop != null) {
                    operations.Push(new Operation {
                        token = uop.Value,
                        isUnary = true,
                        order = order,
                        isLogical = false,
                    });
                }
                if (Peek().type == TokenType.OpenParen) {
                    parens++;
                    operations.Push(new Operation {
                        token = Next(),
                        order = 999,
                    });
                    continue;
                }
                ParseTerm();
                while (parens > 0 && Peek().type == TokenType.CloseParen) {
                    var closeParen = Next();
                    parens--;
                    if (PeekSuffix()) {
                        ResolveOperations(-1, condition);
                        ParseSuffix();
                    } else {
                        operations.Push(new Operation {
                            token = closeParen,
                            order = 999,
                        });
                    }
                }
                var bop = GetBOP(out order, out var isLogical);
                if (bop == null) break;
                ResolveOperations(order, isLogical);
                if (isLogical) {
                    C.Test(keepConstantTrue: bop.Value.type == TokenType.And);
                } else if (bop.Value.type == TokenType.DoubleDot) {
                    C.ConcatArg();
                }
                operations.Push(new Operation {
                    token = bop.Value,
                    isUnary = false,
                    order = order,
                    isLogical = isLogical,
                });
            }
            if (parens > 0) throw new Exception("Unbalanced parentheses");

            if (operations.Count > 0) {
                ResolveOperations(-1, condition);
            }
            // TODO: Tidy this up a bit?
            while (operations.Count > 0 && operations.Peek().token.type == TokenType.OpenParen) {
                operations.Pop();
            }
            if (operations.Count > 0) {
                throw new InvalidOperationException("Unbalanced operations");
            }
        }

        void ParseTableConstructor() {
            int nArray = 0;
            int nArrayTotal = 0;
            int nHash = 0;
            bool argPending = false;
            C.NewTable();
            while (Peek().type != TokenType.CloseBrace) {
                if (argPending) {
                    C.Argument();
                    argPending = false;
                }
                switch (Peek().type) {
                    case TokenType.Identifier: {
                        var key = Next();
                        if (TryTake(TokenType.Equal)) {
                            C.Constant(key.text!);
                            C.Index();
                            ParseExpression();
                            C.Assign(1, 1);
                            nHash++;
                        } else {
                            lexer.PushBack(key);
                            ParseExpression();
                            argPending = true;
                            nArray++;
                            nArrayTotal++;
                        }
                        break;
                    }
                    case TokenType.OpenBracket: {
                        Next();
                        ParseExpression();
                        Expect(TokenType.CloseBracket, "table index expression");
                        Expect(TokenType.Equal, "table index");
                        C.Index();
                        ParseExpression();
                        C.Assign(1, 1);
                        nHash++;
                        break;
                    }
                    default:
                        ParseExpression();
                        nArray++;
                        nArrayTotal++;
                        if (nArray >= Lua.FieldsPerFlush) {
                            C.FlushTable(nArrayTotal);
                            nArray = 0;
                        } else {
                            argPending = true;
                        }
                        break;
                }
                if (!TryTake(TokenType.Comma)) {
                    break;
                }
            }
            Expect(TokenType.CloseBrace, "table constructor");
            C.SetList(nArrayTotal, nHash, argPending);
        }

        readonly List<string> tmpLocals = new List<string>();

        void ParseLocal() {
            Next();
            if (TryTake(TokenType.Function)) {
                var name = Expect(TokenType.Identifier, "function name")!;
                var scope = C.Closure();
                C.InitLocals(1, 1);
                C.DefineLocal(name); // Local functions become 'active' immediately
                ParseFunction(hasSelf: false, scope);
                return;
            }
            do {
                tmpLocals.Add(Expect(TokenType.Identifier, "local declaration")!);
            } while (TryTake(TokenType.Comma));
            int argc = 0;
            if (TryTake(TokenType.Equal)) {
                argc = ParseArgumentList(0);
            }
            C.InitLocals(tmpLocals.Count, argc);
            foreach (var l in tmpLocals) {
                C.DefineLocal(l);
            }
            tmpLocals.Clear();
        }

        void ParseFunction(bool hasSelf, Compiler scope) {
            scope.PushScope();
            Expect(TokenType.OpenParen, "function");
            if (hasSelf) {
                scope.Reserve("self");
            }
            while (Peek().type != TokenType.CloseParen) {
                if (TryTake(TokenType.TripleDot)) {
                    scope.IsVaradic = true;
                    if (Peek().type != TokenType.CloseParen) {
                        throw new Exception($"Expected ')' but got {Peek()}");
                    }
                    break;
                }
                scope.Reserve(Expect(TokenType.Identifier, "function argument")!);
                if (Peek().type == TokenType.Comma) {
                    Next();
                } else if (Peek().type != TokenType.CloseParen) {
                    throw new Exception($"Expected ',' or ')' but got {Peek()}");
                }
            }
            Next();
            scope.PushScope();
            new Parser(lexer, scope).ParseBlock();
            scope.Return(0);
            scope.PopScope();
            scope.PopScope();
            C.EndClosure(scope);
        }

        int ParseArgumentList(int argc) {
            ParseExpression();
            argc++;
            while (TryTake(TokenType.Comma)) {
                C.Argument();
                ParseExpression();
                argc++;
            }
            return argc;
        }
    }
}