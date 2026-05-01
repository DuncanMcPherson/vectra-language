using VectraLang.Ast.Tokens;

namespace VectraLang.Ast.AstNodes;

public sealed record IntegerLiteralExpr(int Value, TokenLocation Location) 
    : Expr(Location);

public sealed record FloatLiteralExpr(float Value, TokenLocation Location)
    : Expr(Location);

public sealed record StringLiteralExpr(string Value, TokenLocation Location)
    : Expr(Location);

public sealed record BoolLiteralExpr(bool Value, TokenLocation Location)
    : Expr(Location);

public sealed record NullLiteralExpr(TokenLocation Location)
    : Expr(Location);