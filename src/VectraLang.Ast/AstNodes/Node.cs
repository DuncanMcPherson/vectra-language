using VectraLang.Core;

namespace VectraLang.Ast.AstNodes;

public abstract record Node(TokenLocation Location);
public abstract record Expr(TokenLocation Location) : Node(Location);
public abstract record Stmt(TokenLocation Location) : Node(Location);