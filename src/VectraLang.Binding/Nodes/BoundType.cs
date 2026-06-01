using VectraLang.Ast.AstNodes;

namespace VectraLang.Binding.Nodes;

public abstract record BoundType;

public sealed record BoundPrimitiveType(string Name) : BoundType;

public sealed record BoundUserDefinedType(
    string QualifiedName,
    ITopLevelDecl Declaration) : BoundType;

public sealed record BoundGenericType(
    string QualifiedName,
    ITopLevelDecl Declaration,
    List<BoundType> TypeArguments) : BoundType;

public sealed record BoundInferredType : BoundType;

public sealed record BoundErrorType(string Name) : BoundType;