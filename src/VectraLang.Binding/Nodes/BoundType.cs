using VectraLang.Ast.AstNodes;

namespace VectraLang.Binding.Nodes;

public abstract record BoundType
{
    public abstract string DisplayName { get; }
    public sealed override string ToString() => DisplayName;
}

public sealed record BoundPrimitiveType(string Name) : BoundType
{
    public override string DisplayName => Name;
}

public sealed record BoundUserDefinedType(
    string QualifiedName,
    ITopLevelDecl Declaration) : BoundType
{
    public override string DisplayName => QualifiedName;
}

public sealed record BoundGenericType(
    string QualifiedName,
    ITopLevelDecl Declaration,
    List<BoundType> TypeArguments) : BoundType
{
    public override string DisplayName => $"{QualifiedName}<{string.Join(", ", TypeArguments)}>";
}

public sealed record BoundInferredType : BoundType
{
    public override string DisplayName => "unknown";
}

public sealed record BoundErrorType(string Name) : BoundType
{
    public override string DisplayName => $"error({Name})";
}