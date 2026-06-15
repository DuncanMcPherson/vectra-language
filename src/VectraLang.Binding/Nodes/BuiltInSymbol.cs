namespace VectraLang.Binding.Nodes;

public abstract record BoundBuiltInSymbol(string Name) : BoundNode;

public sealed record BoundBuiltInFunction(
    string Name,
    BoundType ReturnType,
    List<BoundParameter> Parameters) : BoundBuiltInSymbol(Name), IBoundInvocable;

public sealed record BoundBuiltInMethod(
    string Name,
    BoundType ReturnType,
    List<BoundParameter> Parameters) : BoundBuiltInSymbol(Name), IBoundInvocable;