namespace VectraLang.Ast.Tokens;

public enum TokenType
{
    // 1. Literals
    Int,
    Float,
    String,
    Bool,
    Null,

    // 2. Operators
    // 2.1 Arithmetic
    Plus,
    Minus,
    Star,
    Slash,
    Percent,

    // 2.2 Comparison
    EqualEqual,
    BangEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,

    // 2.3 Logical
    AmpAmp,
    PipePipe,
    Bang,

    // 2.4 Assignment
    Equal,
    // Deferred (post-v1)
    // PlusEqual, MinusEqual, StarEqual, SlashEqual  — compound assignment
    // QuestionQuestion                              — null coalescing
    // FatArrow                                      — =>
    // 3. Delimiters
    LeftParen,
    RightParen,
    LeftBrace,
    RightBrace,
    LeftBracket,
    RightBracket,
    Comma, 
    Dot,
    Semicolon,
    Colon,
    QuestionDot,
    // 4. Keywords
    Space,
    Void,
    Class,
    Enum,
    Interface,
    Let,
    If,
    Else,
    For,
    While,
    Return,
    Break,
    Continue,
    New,
    This,
    Static,
    Public,
    Private,
    Protected,
    Async,
    Await,
    True,
    False,
    Identifier,
    EndOfFile,
    Error
}