namespace VectraLang.Interpreter;

public sealed class VectraEnvironment
{
    private readonly Dictionary<string, object?> _values = [];
    private readonly VectraEnvironment? _parent;

    public VectraEnvironment(VectraEnvironment? parent = null)
    {
        _parent = parent;
    }

    public void Define(string name, object? value)
    {
        _values[name] = value;
    }
    
    public object? Get(string name)
    {
        if (_values.TryGetValue(name, out object? value))
            return value;

        if (_parent is not null)
            return _parent.Get(name);

        throw new RuntimeException($"Undefined variable '{name}'.");
    }

    public void Assign(string name, object? value)
    {
        if (_values.ContainsKey(name))
        {
            _values[name] = value;
            return;
        }

        if (_parent is not null)
        {
            _parent.Assign(name, value);
            return;
        }

        throw new RuntimeException($"Undefined variable '{name}'.");
    }

    public VectraEnvironment CreateChild() => new(this);

    public bool IsDefined(string name) =>
        _values.ContainsKey(name) || (_parent?.IsDefined(name) ?? false);
}