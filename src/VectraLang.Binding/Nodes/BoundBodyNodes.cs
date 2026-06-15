using VectraLang.Ast.AstNodes;
using VectraLang.Ast.Tokens;
using VectraLang.Core;

namespace VectraLang.Binding.Nodes;

public abstract record BoundCallableBody : BoundNode;

public sealed record BoundMethodBody(
    List<BoundStmt> Statements,
    MethodDecl Source) : BoundCallableBody;

public sealed record BoundConstructorBody(
    List<BoundStmt> Statements,
    ConstructorDecl Source) : BoundCallableBody;

public sealed record BoundPropertyGetterBody(
    List<BoundStmt> Statements,
    PropertyDecl Source) : BoundCallableBody;

public sealed record BoundPropertySetterBody(
    List<BoundStmt> Statements,
    PropertyDecl Source) : BoundCallableBody;

public abstract record BoundStmt(TokenLocation Location) : BoundNode;

public sealed record BoundBlockStmt(List<BoundStmt> Statements, TokenLocation Location) : BoundStmt(Location);

public sealed record BoundVarDeclStmt(string Name, BoundType Type, BoundExpr? Initializer, TokenLocation Location) : BoundStmt(Location);

public sealed record BoundExprStmt(BoundExpr Expression, TokenLocation Location) : BoundStmt(Location);

public sealed record BoundIfStmt(
    BoundExpr Condition,
    BoundStmt ThenBranch,
    BoundStmt? ElseBranch,
    TokenLocation Location) : BoundStmt(Location);

public sealed record BoundWhileStmt(
    BoundExpr Condition,
    BoundStmt Body,
    TokenLocation Location) : BoundStmt(Location);

public sealed record BoundForStmt(
    BoundStmt? Initializer,
    BoundExpr? Condition,
    BoundExpr? Increment,
    BoundStmt Body,
    TokenLocation Location) : BoundStmt(Location);

public sealed record BoundReturnStmt(
    BoundExpr? Value,
    BoundType? ExpectedType,
    TokenLocation Location) : BoundStmt(Location);

public sealed record BoundBreakStmt(TokenLocation Location) : BoundStmt(Location);

public sealed record BoundContinueStmt(TokenLocation Location) : BoundStmt(Location);

public sealed record BoundErrorStmt(TokenLocation Location) : BoundStmt(Location);

// Expressions
public abstract record BoundExpr(BoundType Type, TokenLocation Location) : BoundNode;

public sealed record BoundBinaryExpr(
    BoundExpr Left,
    Token Operator,
    BoundExpr Right,
    BoundType Type,
    TokenLocation Location) : BoundExpr(Type, Location);

public sealed record BoundUnaryExpr(
    Token Operator,
    BoundExpr Operand,
    BoundType Type,
    TokenLocation Location) : BoundExpr(Type, Location);

public sealed record BoundGroupingExpr(
    BoundExpr Inner,
    BoundType Type,
    TokenLocation Location) : BoundExpr(Type, Location);

public sealed record BoundVariableExpr(
    string Name,
    BoundType Type,
    TokenLocation Location) : BoundExpr(Type, Location);

public sealed record BoundAssignExpr(
    BoundExpr Target,
    BoundExpr Value,
    BoundType Type,
    TokenLocation Location) : BoundExpr(Type, Location);

public sealed record BoundCallExpr(
    BoundExpr Callee,
    List<BoundExpr> Arguments,
    IBoundInvocable? ResolvedTarget,
    BoundType Type,
    TokenLocation Location) : BoundExpr(Type, Location);

public sealed record BoundGetExpr(
    BoundExpr Object,
    string MemberName,
    BoundType Type,
    TokenLocation Location) : BoundExpr(Type, Location);

public sealed record BoundNewExpr(
    BoundType TargetType,
    List<BoundExpr> Arguments,
    BoundConstructor? ResolvedConstructor,
    BoundType Type,
    TokenLocation Location) : BoundExpr(Type, Location);

public sealed record BoundErrorExpr(
    BoundType Type,
    TokenLocation Location) : BoundExpr(Type, Location);
    
public sealed record BoundIntegerLiteralExpr(
    int Value,
    BoundType Type,
    TokenLocation Location) : BoundExpr(Type, Location);

public sealed record BoundFloatLiteralExpr(
    float Value,
    BoundType Type,
    TokenLocation Location) : BoundExpr(Type, Location);

public sealed record BoundStringLiteralExpr(
    string Value,
    BoundType Type,
    TokenLocation Location) : BoundExpr(Type, Location);

public sealed record BoundBoolLiteralExpr(
    bool Value,
    BoundType Type,
    TokenLocation Location) : BoundExpr(Type, Location);

public sealed record BoundNullLiteralExpr(
    BoundType Type,
    TokenLocation Location) : BoundExpr(Type, Location);