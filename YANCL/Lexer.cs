using System;
using System.Collections.Generic;
using System.Text;

namespace YANCL
{
    class Lexer
    {
        public Lexer(string source) {
            str = source;
        }

        int position;
        string str;

        Stack<Token> tokens = new Stack<Token>();

        public void PushBack(Token token) => tokens.Push(token);

        public Token Next() {
            if (tokens.Count > 0) {
                return tokens.Pop();
            }

            if (position >= str.Length) {
                return TokenType.Eof;
            }

            var start = position;
            char c = str[position++];
            if (char.IsWhiteSpace(c)) {
                while (position < str.Length && char.IsWhiteSpace(str[position])) {
                    position++;
                }
                return Next();
            }

            if (char.IsLetter(c) || c == '_') {
                while (position < str.Length && (char.IsLetter(str[position]) || char.IsDigit(str[position]) || str[position] == '_')) {
                    position++;
                }
                var identifier = str.Substring(start, position - start);
                switch (identifier) {
                    case "and": return TokenType.And;
                    case "or": return TokenType.Or;
                    case "not": return TokenType.Not;
                    case "if": return TokenType.If;
                    case "then": return TokenType.Then;
                    case "else": return TokenType.Else;
                    case "elseif": return TokenType.ElseIf;
                    case "for": return TokenType.For;
                    case "do": return TokenType.Do;
                    case "while": return TokenType.While;
                    case "end": return TokenType.End;
                    case "return": return TokenType.Return;
                    case "local": return TokenType.Local;
                    case "function": return TokenType.Function;
                    case "true": return TokenType.True;
                    case "false": return TokenType.False;
                    case "nil": return TokenType.Nil;
                    case "break": return TokenType.Break;
                    case "repeat": return TokenType.Repeat;
                    case "until": return TokenType.Until;
                    case "in": return TokenType.In;
                    default: return new Token {
                        type = TokenType.Identifier,
                        text = identifier
                    };
                }
            }

            if (char.IsDigit(c) || (c == '.' && position < str.Length && char.IsDigit(str[position]))) {
                position--;
                bool hasDp = false;
                bool end = false;
                bool hasMantissa = false;
                bool negativeExponent = false;
                long mantissa = 0;
                int exponent = 0;
                while (!end && position < str.Length) {
                    switch (str[position]) {
                        case '.':
                            if (hasDp || hasMantissa) {
                                end = true;
                                break;
                            }
                            hasDp = true;
                            position++;
                            break;
                        case 'e':
                        case 'E':
                            if (hasMantissa) {
                                end = true;
                                break;
                            }
                            hasMantissa = true;
                            position++;
                            if (position < str.Length) {
                                switch (str[position]) {
                                    case '-':
                                        negativeExponent = true;
                                        position++;
                                        break;
                                    case '+':
                                        position++;
                                        break;
                                }
                            }
                            break;
                        case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': case '8': case '9':
                            if (hasMantissa) {
                                exponent = exponent * 10 + ((str[position] - '0') * (negativeExponent ? -1 : 1));
                                position++;
                            } else {
                                mantissa = mantissa * 10 + (str[position] - '0');
                                if (hasDp) {
                                    exponent -= 1;
                                }
                                position++;
                            }
                            break;
                        default:
                            end = true;
                            break;
                    }
                }
                return new Token {
                    type = TokenType.Number,
                    number = mantissa * Math.Pow(10, exponent)
                };
            }

            TokenType TwoCharToken(TokenType token, char c, TokenType token2) {
                if (position < str.Length && str[position] == c) {
                    position++;
                    return token2;
                }
                return token;
            }


            switch (c) {
                case '\'': return TokenType.SingleQuote;
                case '"': return TokenType.DoubleQuote;
                case '(': return TokenType.OpenParen;
                case ')': return TokenType.CloseParen;
                case '[': return TokenType.OpenBracket;
                case ']': return TokenType.CloseBracket;
                case '{': return TokenType.OpenBrace;
                case '}': return TokenType.CloseBrace;
                case ',': return TokenType.Comma;
                case '.':
                    if (position < str.Length && str[position] == '.') {
                        position++;
                        if (position < str.Length && str[position] == '.') {
                            position++;
                            return TokenType.TripleDot;
                        }
                        return TokenType.DoubleDot;
                    }
                    return TokenType.Dot;
                case ':': return TokenType.Colon;
                case ';': return TokenType.Semicolon;
                case '+': return TokenType.Plus;
                case '-': return TokenType.Minus;
                case '*': return TokenType.Star;
                case '/': return TokenType.Slash;
                case '%': return TokenType.Percent;
                case '^': return TokenType.Caret;
                case '~': return TwoCharToken(TokenType.Tilde, '=', TokenType.NotEqual);
                case '=': return TwoCharToken(TokenType.Equal, '=', TokenType.DoubleEqual);
                case '<': return TwoCharToken(TokenType.LessThan, '=', TokenType.LessThanEqual);
                case '>': return TwoCharToken(TokenType.GreaterThan, '=', TokenType.GreaterThanEqual);
                case '#': return TokenType.Hash;
                default:
                    throw new Exception($"Unexpected character '{c}' at position {position}");
            }
        }

        public string ParseString(char term) {
            var sb = new StringBuilder();
            while (true) {
                var c = str[position++];
                if (c == '\\') {
                    c = str[position++];
                    switch (c) {
                        case 'a': sb.Append('\a'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'v': sb.Append('\v'); break;
                        case '\\': sb.Append('\\'); break;
                        case '"': sb.Append('"'); break;
                        case '\'': sb.Append('\''); break;
                        case '\n': break;
                        default:
                            throw new Exception($"Invalid escape sequence '\\{c}'");
                    }
                } else if (c == term) {
                    break;
                } else {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public TokenType Peek() {
            if (tokens.Count == 0) {
                tokens.Push(Next());
            }
            return tokens.Peek().type;
        }

        public string? Expect(TokenType type, string after) {
            var token = Next();
            if (token.type != type) {
                throw new Exception($"Expected {type} after {after}, but got {token.type}");
            }
            return token.text;
        }

        public bool TryTake(TokenType type) {
            if (Peek() == type) {
                Next();
                return true;
            }
            return false;
        }
    }
}
