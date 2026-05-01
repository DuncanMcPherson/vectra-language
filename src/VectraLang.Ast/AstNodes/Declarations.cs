using VectraLang.Ast.Tokens;

namespace VectraLang.Ast.AstNodes;

public interface ITopLevelDecl;

public interface ICallable;

public sealed record VectraFile(
    SpaceDecl Space,
    List<ITopLevelDecl> Declarations,
    TokenLocation Location) : Node(Location);

public sealed record SpaceDecl(
    Token Name,
    TokenLocation Location) : Node(Location);

public sealed record ParameterNode(
    TypeNode Type,
    Token Name,
    TokenLocation Location) : Node(Location);

public sealed record MethodDecl(
    Token Name,
    TypeNode ReturnType,
    List<ParameterNode> Parameters,
    BlockStmt Body,
    List<Token> Modifiers,
    TokenLocation Location) : Node(Location);

public sealed record ConstructorDecl(
    Token Name, // Should always be the same as the class name
    List<ParameterNode> Parameter,
    BlockStmt Body,
    List<Token> Modifiers,
    TokenLocation Location) : Node(Location), ICallable;

public sealed record PropertyDecl(
    TypeNode Type,
    Token Name,
    Expr? Getter,
    Expr? Setter,
    List<Token> Modifiers,
    TokenLocation Location) : Node(Location);

public sealed record ClassDecl(
    Token Name,
    List<PropertyDecl> Properties,
    List<MethodDecl> Methods,
    List<ConstructorDecl> Constructors,
    List<Token> Modifiers,
    TokenLocation Location) : Node(Location), ITopLevelDecl;

public sealed record EnumVariantNode(
    Token Name,
    List<Expr> Arguments,
    List<MethodDecl> Overrides,
    TokenLocation Location) : Node(Location);

public sealed record EnumDecl(
    Token Name,
    List<ParameterNode> Parameters,
    List<EnumVariantNode> Variants,
    List<MethodDecl> Methods,
    List<Token> Modifiers,
    TokenLocation Location) : Node(Location), ITopLevelDecl;

public sealed record MethodSignatureDecl(
    Token Name,
    TypeNode ReturnType,
    List<ParameterNode> Parameters,
    TokenLocation Location) : Node(Location);

public sealed record InterfaceDecl(
    Token Name,
    List<MethodSignatureDecl> Methods,
    List<Token> Modifiers,
    TokenLocation Location) : Node(Location), ITopLevelDecl;