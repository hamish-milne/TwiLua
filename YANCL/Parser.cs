

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace YANCL
{
    public class Parser
    {
        readonly Parser? parent;
        readonly Lexer lexer;
        readonly List<string> locals = new List<string>();
        readonly List<LuaFunction> prototypes = new List<LuaFunction>();
        readonly ICompiler2 C = new Compiler22();

        Token Next() => lexer.Next();
        TokenType Peek() => lexer.Peek();
        string? Expect(TokenType type, string after) => lexer.Expect(type, after);
        string ParseString(char term) => lexer.ParseString(term);
        bool TryTake(TokenType type) => lexer.TryTake(type);


        Parser(string str) {
            lexer = new Lexer(str);
        }

        Parser(Parser parent) {
            this.parent = parent;
            lexer = parent.lexer;
        }

        bool PeekSuffix() {
            switch (Peek()) {
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

        void ParseStat() {
            switch (Peek()) {
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
                    var name = Expect(TokenType.Identifier, "function name")!;
                    C.Upvalue(0);
                    C.Constant(name);
                    C.Index();
                    ParseFunction(hasSelf: false);
                    C.Assign(1, 1);
                    break;
                case TokenType.Return:
                    Next();
                    int argc = 0;
                    switch (Peek()) {
                        case TokenType.Eof:
                        case TokenType.Semicolon:
                        case TokenType.End:
                            break;
                        default:
                            argc = ParseArgumentList();
                            break;
                    }
                    C.Return(argc);
                    while (Peek() == TokenType.Semicolon) {
                        Next();
                    }
                    switch (Peek()) {
                        case TokenType.End:
                        case TokenType.Eof:
                            break;
                        default:
                            throw new Exception($"Unexpected {Peek()} after return");
                    }
                    break;
                default:
                    throw new Exception($"Unexpected token {Peek()}");
            }
        }

        void ParseIdentifier(Token token) {
            var name = token.text!;
            var localIdx = locals.IndexOf(name);
            if (localIdx < 0) { // Global
                C.Upvalue(0);
                C.Constant(name);
                C.Index();
            } else {
                C.Local(localIdx);
            }
        }

        void ParseVar(int nTargets) {
            var token = Next();
            switch (token.type) {
                case TokenType.Identifier: {
                    ParseIdentifier(token);
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
                    int argc = ParseArgumentList();
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
            switch (Peek()) {
                case TokenType.Dot:
                    // C.Indexee();
                    Next();
                    var name = Expect(TokenType.Identifier, "dot")!;
                    C.Constant(name);
                    C.Index();
                    endsInCall = false;
                    ParseSuffix(ref endsInCall);
                    break;
                case TokenType.OpenBracket:
                    // C.Indexee();
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
                    int argc = 0;
                    if (!TryTake(TokenType.CloseParen)) {
                        argc = ParseArgumentList();
                        Expect(TokenType.CloseParen, "argument list");
                    }
                    C.Call(argc);
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

        void ParseTerm() {
            var token = Next();
            switch (token.type) {
                case TokenType.Identifier:
                    ParseIdentifier(token);
                    ParseSuffix();
                    break;
                case TokenType.Number: C.Constant(token.number); break;
                case TokenType.DoubleQuote: C.Constant(ParseString('"')); break;
                case TokenType.SingleQuote: C.Constant(ParseString('\'')); break;
                case TokenType.OpenParen: {
                    ParseExpression();
                    Expect(TokenType.CloseParen, "parenthesized expression");
                    ParseSuffix();
                    break;
                }
                case TokenType.True: C.Constant(true); break;
                case TokenType.False: C.Constant(false); break;
                case TokenType.Nil: C.Constant(LuaValue.Nil); break;
                case TokenType.OpenBrace: ParseTableConstructor(); break;
                case TokenType.TripleDot: C.Vararg(); break;
                case TokenType.Function: ParseFunction(hasSelf: false); break;
                default: throw new Exception($"Unexpected token {token.type}");
            }
        }

        struct Operation {
            public bool isUnary;
            public int order;
            public TokenType token;
        }

        readonly Stack<Operation> operations = new Stack<Operation>();

        TokenType? GetUOP(out int order) {
            order = 10;
            var token = Peek();
            switch (token) {
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

        TokenType? GetBOP(out int order) {
            order = -1;
            var token = Peek();
            order = token switch {
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
                TokenType.Percent => 9,
                TokenType.Caret => 11,
                _ => -1
            };
            if (order > 0) {
                Next();
                return token;
            }
            return null;
        }

        void ResolveOperations(int order) {
            while (operations.Count > 0 && operations.Peek().order >= order) {
                var op = operations.Pop();
                if (op.isUnary) {
                    C.Unary(op.token);
                } else {
                    C.Binary(op.token);
                }
            }
        }

        void ParseExpression() {
            while (true) {
                var uop = GetUOP(out var order);
                ParseTerm();
                if (uop != null) {
                    ResolveOperations(order);
                    operations.Push(new Operation {
                        token = uop.Value,
                        isUnary = true,
                        order = order
                    });
                }
                var bop = GetBOP(out order);
                if (bop == null) break;
                ResolveOperations(order);
                operations.Push(new Operation {
                    token = bop.Value,
                    isUnary = false,
                    order = order
                });
            }

            if (operations.Count > 0) {
                ResolveOperations(-1);
            }
            Debug.Assert(operations.Count == 0);
        }

        void ParseTableConstructor() {
            int nArray = 0;
            int nHash = 0;
            C.NewTable();
            if (TryTake(TokenType.CloseBrace)) {
                return;
            }
            do {
                switch (Peek()) {
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
                            C.Argument();
                            nArray++;
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
                        C.Argument();
                        nArray++;
                        break;
                }
            } while (TryTake(TokenType.Comma) && Peek() != TokenType.CloseBrace);
            Expect(TokenType.CloseBrace, "table constructor");
            C.SetList(nArray, nHash);
        }

        readonly List<string> tmpLocals = new List<string>();

        void ParseLocal() {
            Next();
            if (TryTake(TokenType.Function)) {
                var name = Expect(TokenType.Identifier, "function name")!;
                ParseFunction(hasSelf: false);
                C.InitLocals(1, 1);
                locals.Add(name);
                return;
            }
            do {
                tmpLocals.Add(Expect(TokenType.Identifier, "local declaration")!);
                if (locals.Contains(tmpLocals[tmpLocals.Count - 1])) {
                    throw new Exception("Local re-declaration");
                }
            } while (TryTake(TokenType.Comma));
            int argc = 0;
            if (TryTake(TokenType.Equal)) {
                argc = ParseArgumentList();
            }
            C.InitLocals(tmpLocals.Count, argc);
            locals.AddRange(tmpLocals);
            tmpLocals.Clear();
        }

        void ParseFunction(bool hasSelf) {
            Expect(TokenType.OpenParen, "function");
            var scope = new Parser(this);
            if (hasSelf) {
                scope.locals.Add("self");
            }
            while (Peek() != TokenType.CloseParen) {
                scope.locals.Add(Expect(TokenType.Identifier, "function argument")!);
                if (Peek() == TokenType.Comma) {
                    Next();
                } else if (Peek() != TokenType.CloseParen) {
                    throw new Exception($"Expected ',' or ')' but got {Peek()}");
                }
            }
            Next();
            scope.ParseBlock();
            C.Closure(scope.MakeFunction());
        }

        LuaFunction MakeFunction() => C.MakeFunction();

        int ParseArgumentList() {
            ParseExpression();
            int argc = 1;
            while (TryTake(TokenType.Comma)) {
                C.Argument();
                ParseExpression();
                argc++;
            }
            return argc;
        }

        public static LuaFunction Compile(string str) {
            var c = new Parser(str);
            c.ParseChunk();
            return c.MakeFunction();
        }
    }
}