using VectraLang.Binding.Nodes;

namespace VectraLang.Binding.Scope;

public class LocalScope
{
    private readonly LocalScope? _parent;
    private readonly Dictionary<string, BoundType> _locals = new();

    public LocalScope(LocalScope? parent = null)
    {
        _parent = parent;
    }
    
    public bool TryDeclare(string name, BoundType type) => _locals.TryAdd(name, type);

    public bool TryResolve(string name, out BoundType? type)
    {
        if (_locals.TryGetValue(name, out type))
            return true;

        return _parent is not null && _parent.TryResolve(name, out type);
    }

    public LocalScope CreateChildScope() => new(this);
}