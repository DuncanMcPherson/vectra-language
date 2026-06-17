namespace VectraLang.Bytecode;

public sealed class MethodTable
{
    private readonly List<LoweredMethod> _methods = [];

    public ushort Add(LoweredMethod method)
    {
        _methods.Add(method);
        return (ushort)(_methods.Count - 1);
    }

    public LoweredMethod Get(ushort index) => _methods[index];
    public IReadOnlyList<LoweredMethod> All => _methods;
    public void Replace(ushort index, LoweredMethod method) => _methods[index] = method;
}