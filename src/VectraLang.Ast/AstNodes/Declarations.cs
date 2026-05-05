using VectraLang.Ast.Tokens;

namespace VectraLang.Ast.AstNodes;

public interface ITopLevelDecl;

public interface ICallable;

public sealed record VectraFile(
    SpaceDecl Space,
    List<EnterDecl> EnterDeclarations,
    TokenLocation Location) : Node(Location);

public sealed record EnterDecl(
    string QualifiedName,
    TokenLocation Location) : Node(Location);

public sealed record SpaceDecl(
    Token Name,
    SpaceDecl? Parent,
    List<SpaceDecl> Children,
    List<ITopLevelDecl> Declarations,
    TokenLocation Location) : Node(Location)
{
    public string QualifiedName => Parent is not null
        ? $"{Parent.QualifiedName}.{Name.Lexeme}"
        : Name.Lexeme;
    
    public void AddDeclaration(ITopLevelDecl declaration) => Declarations.Add(declaration);
    public void AddChild(SpaceDecl child) => Children.Add(child);
}

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
    List<ParameterNode> Parameters,
    BlockStmt Body,
    List<Token> Modifiers,
    TokenLocation Location) : Node(Location), ICallable;

public sealed record FieldDecl(
    TypeNode Type,
    Token Name,
    Expr? Initializer,
    List<Token> Modifiers,
    TokenLocation Location) : Node(Location);

public sealed record PropertyDecl(
    TypeNode Type,
    Token Name,
    BlockStmt? Getter,
    BlockStmt? Setter,
    List<Token> Modifiers,
    TokenLocation Location) : Node(Location);

public sealed record ClassDecl(
    Token Name,
    List<PropertyDecl> Properties,
    List<MethodDecl> Methods,
    List<ConstructorDecl> Constructors,
    List<FieldDecl> Fields,
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