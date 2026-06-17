namespace VectraLang.Bytecode;

public sealed class ConstantPool
{
    private readonly List<object> _constants = [];

    public ushort AddInt(int value) => Add(value);
    public ushort AddFloat(float value) => Add(value);
    public ushort AddString(string value) => Add(value);

    private ushort Add(object value)
    {
        var existing = _constants.IndexOf(value);
        if (existing >= 0) return (ushort)existing;
        
        _constants.Add(value);
        return (ushort)(_constants.Count - 1);
    }

    public object Get(ushort index) => _constants[index];
    public IReadOnlyList<object> All => _constants;
}