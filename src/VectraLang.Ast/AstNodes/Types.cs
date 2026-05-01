using VectraLang.Ast.Tokens;

namespace VectraLang.Ast.AstNodes;

public abstract record TypeNode(TokenLocation Location) : Node(Location);

public sealed record PrimitiveTypeNode(
    Token TypeToken,
    TokenLocation Location) : TypeNode(Location);

public sealed record GenericTypeNode(
    Token TypeToken,
    List<TypeNode> TypeArguments,
    TokenLocation Location) : TypeNode(Location);

public sealed record InferredTypeNode(
    TokenLocation Location) : TypeNode(Location);    