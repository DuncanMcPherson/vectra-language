namespace VectraLang.Bytecode;

public record LoweredType(
    string QualifiedName,
    List<LoweredField> Field,
    List<ushort> MethodIndices);

public sealed record LoweredField(
    string Name,
    string TypeName);

public sealed record LoweredMethod(
    string Name,
    ushort[] Bytecode,
    ushort LocalCount,
    ushort ParameterCount);    