using VectraLang.Ast.Tokens;
using VectraLang.Core;

namespace VectraLang.Ast.AstNodes;

public sealed record VarDeclStmt(
    TypeNode Type,           // PrimitiveTypeNode, GenericTypeNode, or InferredTypeNode
    Token Name,
    Expr? Initializer,       // null when int z; form is used
    TokenLocation Location) : Stmt(Location);

public sealed record ErrorStmt(TokenLocation Location) : Stmt(Location);

public sealed record ExprStmt(
    Expr Expression,
    TokenLocation Location) : Stmt(Location);

public sealed record BlockStmt(
    List<Stmt> Statements,
    TokenLocation Location) : Stmt(Location);

public sealed record IfStmt(
    Expr Condition,
    Stmt ThenBranch,
    Stmt? ElseBranch,
    TokenLocation Location) : Stmt(Location);

public sealed record WhileStmt(
    Expr Condition,
    Stmt Body,
    TokenLocation Location) : Stmt(Location);

public sealed record ForStmt(
    Stmt? Initializer,
    Expr? Condition,
    Expr? Increment,
    Stmt Body,
    TokenLocation Location) : Stmt(Location);

public sealed record ReturnStmt(
    Expr? Value,
    TokenLocation Location) : Stmt(Location);

public sealed record BreakStmt(
    TokenLocation Location) : Stmt(Location);

public sealed record ContinueStmt(
    TokenLocation Location) : Stmt(Location);