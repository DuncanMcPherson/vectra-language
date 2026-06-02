using VectraLang.Core;

namespace VectraLang.Ast.Tokens;

public sealed record Token(
    TokenType Type,
    string Lexeme,
    object? Literal,
    TokenLocation Location)
{
    public override string ToString() => $"({Type}) {Lexeme}";
}