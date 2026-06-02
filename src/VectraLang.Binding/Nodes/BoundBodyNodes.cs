using VectraLang.Ast.AstNodes;
using VectraLang.Ast.Tokens;

namespace VectraLang.Binding.Nodes;

public abstract record BoundCallableBody : BoundNode;

public sealed record BoundMethodBody(
    List<BoundStmt> Statements,
    MethodDecl Source) : BoundCallableBody;

public sealed record BoundConstructorBody(
    List<BoundStmt> Statements,
    ConstructorDecl Source) : BoundCallableBody;

public abstract record BoundStmt : BoundNode;

public sealed record BoundBlockStmt(List<BoundStmt> Statements) : BoundStmt;

public sealed record BoundVarDeclStmt(string Name, BoundType Type, BoundExpr? Initializer) : BoundStmt;

public sealed record BoundExprStmt(BoundExpr Expression) : BoundStmt;

public sealed record BoundIfStmt(
    BoundExpr Condition,
    BoundStmt ThenBranch,
    BoundStmt? ElseBranch) : BoundStmt;

public sealed record BoundWhileStmt(
    BoundExpr Condition,
    BoundStmt Body) : BoundStmt;

public sealed record BoundForStmt(
    BoundStmt? Initializer,
    BoundExpr? Condition,
    BoundExpr? Increment,
    BoundStmt Body) : BoundStmt;

public sealed record BoundReturnStmt(
    BoundExpr? Value,
    BoundType? ExpectedType) : BoundStmt;

public sealed record BoundBreakStmt : BoundStmt;
public sealed record BoundContinueStmt : BoundStmt;

public sealed record BoundErrorStmt : BoundStmt;

// Expressions
public abstract record BoundExpr(BoundType Type) : BoundNode;

public sealed record BoundBinaryExpr(
    BoundExpr Left,
    Token Operator,
    BoundExpr Right,
    BoundType Type) : BoundExpr(Type);

public sealed record BoundUnaryExpr(
    Token Operator,
    BoundExpr Operand,
    BoundType Type) : BoundExpr(Type);

public sealed record BoundGroupingExpr(
    BoundExpr Inner,
    BoundType Type) : BoundExpr(Type);

public sealed record BoundVariableExpr(
    string Name,
    BoundType Type) : BoundExpr(Type);

public sealed record BoundAssignExpr(
    BoundExpr Target,
    BoundExpr Value,
    BoundType Type) : BoundExpr(Type);

public sealed record BoundCallExpr(
    BoundExpr Callee,
    List<BoundExpr> Arguments,
    BoundType Type) : BoundExpr(Type);

public sealed record BoundGetExpr(
    BoundExpr Object,
    string MemberName,
    BoundType Type) : BoundExpr(Type);

public sealed record BoundNewExpr(
    BoundType TargetType,
    List<BoundExpr> Arguments,
    BoundType Type) : BoundExpr(Type);

public sealed record BoundErrorExpr(
    BoundType Type) : BoundExpr(Type);
    
public sealed record BoundIntegerLiteralExpr(
    int Value,
    BoundType Type) : BoundExpr(Type);

public sealed record BoundFloatLiteralExpr(
    float Value,
    BoundType Type) : BoundExpr(Type);

public sealed record BoundStringLiteralExpr(
    string Value,
    BoundType Type) : BoundExpr(Type);

public sealed record BoundBoolLiteralExpr(
    bool Value,
    BoundType Type) : BoundExpr(Type);

public sealed record BoundNullLiteralExpr(
    BoundType Type) : BoundExpr(Type);