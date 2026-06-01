using VectraLang.Ast.AstNodes;
using VectraLang.Binding.Nodes;

namespace VectraLang.Binding.Scope;

public class BindingScope
{
    private readonly Dictionary<string, ITopLevelDecl> _types = new();
    private readonly Dictionary<string, SpaceDecl> _spaces = new();
    private readonly Dictionary<BoundCallable, BlockStmt> _pendingBodies = new();
    private readonly Dictionary<BoundCallable, BoundCallableBody> _resolvedBodies = new();

    public void RegisterPendingBody(BoundCallable callable, BlockStmt body)
        => _pendingBodies[callable] = body;
    
    public void RegisterResolvedBody(BoundCallable callable, BoundCallableBody body)
        => _resolvedBodies[callable] = body;
    
    public bool TryGetResolvedBody(BoundCallable callable, out BoundCallableBody? body)
         => _resolvedBodies.TryGetValue(callable, out body);

    public IEnumerable<KeyValuePair<BoundCallable, BlockStmt>> GetPendingBodies() => _pendingBodies;

    public bool TryRegisterType(ITopLevelDecl decl)
    {
        var fullName = decl.GetFullName();
        return _types.TryAdd(fullName, decl);
    }

    public bool TryRegisterSpace(SpaceDecl space)
    {
        var fullName = space.QualifiedName;
        return _spaces.TryAdd(fullName, space);
    }

    public bool TryResolveType(string qualifiedName, out ITopLevelDecl? decl)
    {
        return _types.TryGetValue(qualifiedName, out decl);
    }

    public bool TryResolveSpace(string qualifiedName, out SpaceDecl? space)
    {
        return _spaces.TryGetValue(qualifiedName, out space);
    }
}