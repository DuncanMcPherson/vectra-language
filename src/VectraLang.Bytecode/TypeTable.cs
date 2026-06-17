namespace VectraLang.Bytecode;

public sealed class TypeTable
{
    private readonly List<LoweredType> _types = [];

    public ushort Add(LoweredType type)
    {
        _types.Add(type);
        return (ushort)(_types.Count - 1);
    }
    
    public LoweredType Get(ushort index) => _types[index];
    public IReadOnlyList<LoweredType> All => _types;
}