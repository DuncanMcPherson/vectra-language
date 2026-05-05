using VectraLang.Ast.Tokens;

namespace VectraLang.Ast.AstNodes;

public sealed record BinaryExpr(
    Expr Left,
    Token Operator,
    Expr Right,
    TokenLocation Location)
    : Expr(Location);

public sealed record UnaryExpr(Token Operator, Expr Right, TokenLocation Location)
    : Expr(Location);

public sealed record GroupingExpr(
    Expr Inner,
    TokenLocation Location) : Expr(Location);

public sealed record VariableExpr(
    Token Name,
    TokenLocation Location) : Expr(Location);

public sealed record AssignExpr(
    Expr Target,
    Expr Value,
    TokenLocation Location) : Expr(Location);

public sealed record CallExpr(
    Expr Callee,
    List<Expr> Arguments,
    TokenLocation Location) : Expr(Location);

public sealed record GetExpr(
    Expr Object,
    Token Name,
    TokenLocation Location) : Expr(Location);

public sealed record OptionalGetExpr(
    Expr Object,
    Token Name,
    TokenLocation Location) : Expr(Location); // ?.

public sealed record DestructureExpr(
    List<Token> Names,
    Expr Value,
    TokenLocation Location) : Expr(Location);

public sealed record NewExpr(
    Token TypeName,
    List<Expr> Arguments,
    TokenLocation Location) : Expr(Location);    