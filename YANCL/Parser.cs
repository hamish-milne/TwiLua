

using System;
using System.Collections.Generic;
using System.Diagnostics;
using static YANCL.OpCode;

namespace YANCL
{
    public class Parser
    {
        readonly Parser? parent;
        readonly Lexer lexer;
        readonly List<string> locals = new List<string>();
        readonly List<LuaValue> constants = new List<LuaValue>();
        readonly List<LuaFunction> prototypes = new List<LuaFunction>();
        readonly Compiler2 C = new Compiler2();

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

        void ParseVar(int nSlots) {
            var token = Next();
            switch (token.type) {
                case TokenType.Identifier: {
                    ParseIdentifier(token);
                    ContinueParseVar(nSlots);
                    break;
                }
                case TokenType.OpenParen:
                    ParseExpression();
                    Expect(TokenType.CloseParen, "parenthesized expression");
                    if (!PeekSuffix()) {
                        throw new Exception("Expected a token after expression");
                    }
                    ContinueParseVar(nSlots);
                    break;
            }
        }

        void ContinueParseVar(int nSlots) {
            var endsInCall = false;
            ParseSuffix(ref endsInCall);
            if (endsInCall) {
                if (nSlots == 1) {
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
                    C.PushTarget();
                    ParseArgumentList();
                    C.Assign();
                    break;
                case TokenType.Comma:
                    C.PushTarget();
                    ParseVar(nSlots + 1);
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
                    Next();
                    var name = Expect(TokenType.Identifier, "dot")!;
                    C.Constant(name);
                    C.Index();
                    ParseSuffix(ref endsInCall);
                    break;
                case TokenType.OpenBracket:
                    Next();
                    ParseExpression();
                    Expect(TokenType.CloseBracket, "index expression");
                    C.Index();
                    ParseSuffix(ref endsInCall);
                    break;
                case TokenType.OpenParen:
                    Next();
                    if (!TryTake(TokenType.CloseParen)) {
                        ParseArgumentList();
                        Expect(TokenType.CloseParen, "argument list");
                    }
                    C.Call();
                    endsInCall = true;
                    ParseSuffix(ref endsInCall);
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
            C.NewTable();
            while (Peek() != TokenType.CloseBrace) {
                switch (Peek()) {
                    case TokenType.Identifier: {
                        var key = Next();
                        if (TryTake(TokenType.Equal)) {
                            C.Constant(key.text!);
                            C.Index();
                            C.PushTarget();
                            ParseExpression();
                            C.Assign();
                        } else {
                            lexer.PushBack(key);
                            ParseExpression();
                        }
                        break;
                    }
                    case TokenType.OpenBracket: {
                        Next();
                        ParseExpression();
                        Expect(TokenType.CloseBracket, "table index expression");
                        Expect(TokenType.Equal, "table index");
                        C.Index();
                        C.PushTarget();
                        ParseExpression();
                        C.Assign();
                        break;
                    }
                    default:
                        ParseExpression();
                        break;
                }
            }
            C.SetList();
        }

        readonly List<string> tmpLocals = new List<string>();

        void ParseLocal() {
            Next();
            if (TryTake(TokenType.Function)) {
                var name = Expect(TokenType.Identifier, "function name")!;
                ParseFunction(hasSelf: false);
                C.InitLocals(1);
                locals.Add(name);
                return;
            }
            do {
                tmpLocals.Add(Expect(TokenType.Identifier, "local declaration")!);
                if (locals.Contains(tmpLocals[tmpLocals.Count - 1])) {
                    throw new Exception("Local re-declaration");
                }
            } while (TryTake(TokenType.Comma));
            if (TryTake(TokenType.Equal)) {
                ParseArgumentList();
                C.PushArg();
            }
            C.InitLocals(tmpLocals.Count);
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

        void ParseArgumentList() {
            ParseExpression();
            while (TryTake(TokenType.Comma)) {
                C.PushArg();
                ParseExpression();
            }
        }

        public static LuaFunction Compile(string str) {
            var c = new Parser(str);
            c.ParseChunk();
            return c.MakeFunction();
        }
    }
}