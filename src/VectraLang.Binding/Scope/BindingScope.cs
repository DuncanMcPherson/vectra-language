using VectraLang.Ast.AstNodes;
using VectraLang.Binding.Nodes;

namespace VectraLang.Binding.Scope;

public class BindingScope
{
    private readonly Dictionary<string, ITopLevelDecl> _types = new();
    private readonly Dictionary<string, SpaceDecl> _spaces = new();
    private readonly Dictionary<BoundCallable, BlockStmt> _pendingBodies = new();
    private readonly Dictionary<BoundCallable, BoundCallableBody> _resolvedBodies = new();
    private readonly Dictionary<string, BoundBuiltInFunction> _globalFunctions = new();
    private readonly List<BoundBuiltInMethod> _objectMethods = new();
    private readonly Dictionary<string, BoundTypeDecl> _boundTypes = new();

    public void RegisterGlobalFunction(BoundBuiltInFunction fn)
        => _globalFunctions[fn.Name] = fn;

    public void RegisterObjectMethod(BoundBuiltInMethod method)
        => _objectMethods.Add(method);

    public bool TryResolveGlobalFunction(string name, out BoundBuiltInFunction? fn)
        => _globalFunctions.TryGetValue(name, out fn);
    
    public IEnumerable<BoundBuiltInMethod> GetObjectMethods() => _objectMethods;

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
    
    public void RegisterBoundType(BoundTypeDecl type)
        => _boundTypes[type.QualifiedName] = type;

    public bool TryResolveBoundType(string name, out BoundTypeDecl? decl)
    {
        if (_boundTypes.TryGetValue(name, out decl))
            return true;

        var match = _boundTypes.Values.FirstOrDefault(t => t.QualifiedName.Split('.').Last() == name);
        if (match is not null)
        {
            decl = match;
            return true;
        }

        decl = null;
        return false;
    }

    public bool TryResolveMember(BoundType type, string memberName, out BoundType? memberType)
    {
        var objectMethod = _objectMethods.FirstOrDefault(m => m.Name == memberName);
        if (objectMethod is not null)
        {
            memberType = objectMethod.ReturnType;
            return true;
        }

        if (type is BoundUserDefinedType userType)
        {
            if (!TryResolveBoundType(userType.QualifiedName, out var boundTypeDecl) || boundTypeDecl is null)
            {
                memberType = null;
                return false;
            }

            memberType = boundTypeDecl switch
            {
                BoundClass c => ResolveMemberFromBoundClass(c, memberName),
                _ => null
            };
            return memberType is not null;
        }
        
        memberType = null;
        return false;
    }

    private static BoundType? ResolveMemberFromBoundClass(BoundClass decl, string memberName)
    {
        var field = decl.Fields.FirstOrDefault(f => f.Name == memberName);
        if (field is not null)
            return field.Type;
        
        var property = decl.Properties.FirstOrDefault(p => p.Name == memberName);
        if (property is not null) return property.Type;
        
        var method = decl.Methods.FirstOrDefault(m => m.Name == memberName);
        return method?.ReturnType;
    }

    public bool TryRegisterSpace(SpaceDecl space)
    {
        var fullName = space.QualifiedName;
        return _spaces.TryAdd(fullName, space);
    }

    public bool TryResolveType(string qualifiedName, out ITopLevelDecl? decl)
    {
        if (_types.TryGetValue(qualifiedName, out decl))
            return true;
        
        var match = _types.Values.FirstOrDefault(t => t.Name.Lexeme == qualifiedName);

        if (match is not null)
        {
            decl = match;
            return true;
        }

        decl = null;
        return false;
    }

    public bool TryResolveSpace(string qualifiedName, out SpaceDecl? space)
    {
        return _spaces.TryGetValue(qualifiedName, out space);
    }
}