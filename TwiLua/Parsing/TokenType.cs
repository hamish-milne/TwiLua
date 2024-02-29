using System;

namespace TwiLua
{
    enum TokenType {
        Eof,
        Identifier,
        Number,
        SingleQuote,
        DoubleQuote,
        OpenParen,
        CloseParen,
        OpenBracket,
        CloseBracket,
        OpenBrace,
        CloseBrace,
        Comma,
        Dot,
        DoubleDot,
        TripleDot,
        Colon,
        Semicolon,
        Plus,
        Minus,
        Star,
        Slash,
        DoubleSlash,
        Pipe,
        Ampersand,
        Percent,
        Caret,
        Tilde,
        Equal,
        DoubleEqual,
        NotEqual,
        LessThan,
        GreaterThan,
        LessThanEqual,
        GreaterThanEqual,
        ShiftLeft,
        ShiftRight,
        Hash,

        // Keywords
        And,
        Or,
        Not,
        If,
        Then,
        Else,
        ElseIf,
        For,
        Do,
        While,
        End,
        Return,
        Local,
        Function,
        True,
        False,
        Nil,
        Break,
        Repeat,
        Until,
        In,
    }

    readonly struct Token
    {
        public readonly TokenType type;
        public readonly string? text;
        public readonly double number;
        public readonly Location location;

        public Token(TokenType type, Location location) {
            this.type = type;
            text = null;
            number = 0;
            this.location = location;
        }

        public Token(double number, Location location) {
            type = TokenType.Number;
            text = null;
            this.number = number;
            this.location = location;
        }

        public Token(string identifier, Location location) {
            type = TokenType.Identifier;
            text = identifier;
            number = 0;
            this.location = location;
        }

        public override string ToString() => TypeString(type);

        public static string TypeString(TokenType t) => t switch {
            TokenType.Eof => "end of file",
            TokenType.Identifier => "identifier",
            TokenType.Number => "number",
            TokenType.SingleQuote => "single quote",
            TokenType.DoubleQuote => "double quote",
            TokenType.OpenParen => "'('",
            TokenType.CloseParen => "')'",
            TokenType.OpenBracket => "'['",
            TokenType.CloseBracket => "']'",
            TokenType.OpenBrace => "'{'",
            TokenType.CloseBrace => "'}'",
            TokenType.Comma => "','",
            TokenType.Dot => "'.'",
            TokenType.DoubleDot => "'..'",
            TokenType.TripleDot => "'...'",
            TokenType.Colon => "':'",
            TokenType.Semicolon => "';'",
            TokenType.Plus => "'+'",
            TokenType.Minus => "'-'",
            TokenType.Star => "'*'",
            TokenType.Slash => "'/'",
            TokenType.DoubleSlash => "'//'",
            TokenType.Pipe => "'|'",
            TokenType.Ampersand => "'&'",
            TokenType.Percent => "'%'",
            TokenType.Caret => "'^'",
            TokenType.Tilde => "'~'",
            TokenType.Equal => "'='",
            TokenType.DoubleEqual => "'=='",
            TokenType.NotEqual => "'~='",
            TokenType.LessThan => "'<'",
            TokenType.GreaterThan => "'>'",
            TokenType.LessThanEqual => "'<='",
            TokenType.GreaterThanEqual => "'>='",
            TokenType.ShiftLeft => "'<<'",
            TokenType.ShiftRight => "'>>'",
            TokenType.Hash => "'#'",
            TokenType.And => "'and'",
            TokenType.Or => "'or'",
            TokenType.Not => "'not'",
            TokenType.If => "'if'",
            TokenType.Then => "'then'",
            TokenType.Else => "'else'",
            TokenType.ElseIf => "'elseif'",
            TokenType.For => "'for'",
            TokenType.Do => "'do'",
            TokenType.While => "'while'",
            TokenType.End => "'end'",
            TokenType.Return => "'return'",
            TokenType.Local => "'local'",
            TokenType.Function => "'function'",
            TokenType.True => "'true'",
            TokenType.False => "'false'",
            TokenType.Nil => "'nil'",
            TokenType.Break => "'break'",
            TokenType.Repeat => "'repeat'",
            TokenType.Until => "'until'",
            TokenType.In => "'in'",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
