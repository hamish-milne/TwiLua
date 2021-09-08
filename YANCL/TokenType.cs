namespace YANCL
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

    struct Token {
        public TokenType type;
        public string? text;
        public double number;

        public static implicit operator Token(TokenType type) => new Token { type = type };
    }
}
