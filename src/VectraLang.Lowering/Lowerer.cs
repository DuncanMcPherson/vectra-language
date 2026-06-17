using VectraLang.Binding;
using VectraLang.Binding.Nodes;
using VectraLang.Binding.Scope;
using VectraLang.Bytecode;
using VectraLang.Core.Diagnostics;

namespace VectraLang.Lowering;

public class Lowerer(IVectraLogger logger)
{
    private const string Phase = "Lower";

    private ConstantPool _constants = new();
    private TypeTable _types = new();
    private MethodTable _methods = new();
    private ImportTable _imports = new();

    private readonly Dictionary<string, ushort> _methodIndices = new();
    private readonly Dictionary<string, ushort> _typeIndices = new();
    private ushort _localCount = 0;
    private readonly Dictionary<string, ushort> _localSlots = new();

    public LoweredModule Lower(BindingResult result)
    {
        return result.BoundRoot switch
        {
            BoundFile f => LowerFile(f, result.Scope),
            BoundModule m => LowerModule(m, result.Scope),
            _ => throw new InvalidOperationException("Unexpected root node type")
        };
    }

    public List<LoweredModule> LowerPackage(BindingResult result)
    {
        if (result.BoundRoot is not BoundPackage p)
            throw new InvalidOperationException("Expected a package binding result");
        return p.Modules.Select(m => LowerModule(m, result.Scope)).ToList();
    }

    private void ResetForModule()
    {
        _constants = new ConstantPool();
        _types = new TypeTable();
        _methods = new MethodTable();
        _imports = new ImportTable();
        _methodIndices.Clear();
        _typeIndices.Clear();
        ResetLocals();
    }

    private void ResetLocals()
    {
        _localCount = 0;
        _localSlots.Clear();
    }

    private LoweredModule LowerFile(BoundFile file, BindingScope scope)
    {
        ResetForModule();
        logger.Debug(Phase, "Lowering file");

        RegisterSpace(file.Space);
        RegisterCallablesInSpace(file.Space);
        LowerSpace(file.Space, scope);

        return new LoweredModule("__file__", "0", _constants, _types, _methods, _imports);
    }

    private LoweredModule LowerModule(BoundModule module, BindingScope scope)
    {
        ResetForModule();
        logger.Debug(Phase, $"Lowering module '{module.Name}'");

        foreach (var space in module.Spaces)
            RegisterSpace(space);

        foreach (var space in module.Spaces)
            RegisterCallablesInSpace(space);

        foreach (var space in module.Spaces)
            LowerSpace(space, scope);

        return new LoweredModule(module.Name, "0", _constants, _types, _methods, _imports);
    }

    #region Registration

    private void RegisterSpace(BoundSpace space)
    {
        foreach (var decl in space.Declarations)
            RegisterTypeDecl(decl);
        foreach (var child in space.Children)
            RegisterSpace(child);
    }

    private void RegisterTypeDecl(BoundTypeDecl decl)
    {
        switch (decl)
        {
            case BoundClass c:
                RegisterClassType(c);
                break;
            case BoundEnum e:
                logger.Warning(Phase, $"Skipping '{e.QualifiedName}', not implemented.");
                // RegisterEnumType(e, scope);
                break;
            case BoundInterface i:
                logger.Debug(Phase, $"Skipping '{i.QualifiedName}', not implemented.");
                break;
            default:
                throw new InvalidOperationException($"Unexpected type declaration: {decl.GetType()}");
        }
    }

    private void RegisterClassType(BoundClass cls)
    {
        var loweredFields = cls.Fields.Select(f => new LoweredField(f.Name, f.Type.DisplayName)).ToList();
        var type = new LoweredType(cls.QualifiedName, loweredFields, []);
        var index = _types.Add(type);
        _typeIndices.Add(cls.QualifiedName, index);
    }

    private void RegisterCallablesInSpace(BoundSpace space)
    {
        foreach (var decl in space.Declarations)
        {
            switch (decl)
            {
                case BoundClass c:
                    RegisterClassCallables(c);
                    break;
                case BoundEnum e:
                    logger.Warning(Phase, $"Skipping '{e.QualifiedName}', not implemented.");
                    // RegisterEnumCallables(e);
                    break;
                case BoundInterface i:
                    logger.Debug(Phase, $"Skipping '{i.QualifiedName}', not implemented.");
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected type declaration: {decl.GetType()}");
            }
        }

        foreach (var child in space.Children)
            RegisterCallablesInSpace(child);
    }

    private void RegisterClassCallables(BoundClass cls)
    {
        foreach (var method in cls.Methods)
            RegisterCallable(method);
        foreach (var ctor in cls.Constructors)
            RegisterCallable(ctor);

        foreach (var prop in cls.Properties)
        {
            if (prop.Getter is not null)
                RegisterCallable(prop.Getter);
            if (prop.Setter is not null)
                RegisterCallable(prop.Setter);
        }

        if (!_typeIndices.TryGetValue(cls.QualifiedName, out var index))
            throw new InvalidOperationException($"Type '{cls.QualifiedName}' not found in type indices.");
        var type = _types.Get(index);
        var allCallables = cls.Methods.Cast<BoundCallable>()
            .Concat(cls.Constructors)
            .Concat(cls.Properties.Where(p => p.Getter is not null).Select(p => p.Getter!))
            .Concat(cls.Properties.Where(p => p.Setter is not null).Select(p => p.Setter!));
        type.MethodIndices.AddRange(allCallables.Select(c => _methodIndices[GetCallableKey(c)]));
    }

    private void RegisterCallable(BoundCallable callable)
    {
        var key = GetCallableKey(callable);
        var placeholder = new LoweredMethod(callable.Name, [], 0, (ushort)callable.Parameters.Count);
        var index = _methods.Add(placeholder);
        _methodIndices.Add(key, index);
        logger.Debug(Phase, $"Registered callable '{key}' ({index})");
    }

    #endregion

    #region Lowering

    private void LowerSpace(BoundSpace space, BindingScope scope)
    {
        foreach (var decl in space.Declarations)
            LowerDeclaration(decl, scope);
        foreach (var child in space.Children)
            LowerSpace(child, scope);
    }

    private void LowerDeclaration(BoundTypeDecl decl, BindingScope scope)
    {
        switch (decl)
        {
            case BoundClass c:
                LowerClass(c, scope);
                break;
            case BoundEnum:
            case BoundInterface:
                logger.Warning(Phase, $"Skipping '{decl.QualifiedName}', not implemented.");
                break;
            default:
                throw new InvalidOperationException($"Unexpected type declaration: {decl.GetType()}");
        }
    }

    private void LowerClass(BoundClass c, BindingScope scope)
    {
        var allCallables = c.Methods.Cast<BoundCallable>()
            .Concat(c.Constructors)
            .Concat(c.Properties.Where(p => p.Getter is not null).Select(p => p.Getter!))
            .Concat(c.Properties.Where(p => p.Setter is not null).Select(p => p.Setter!));

        foreach (var callable in allCallables)
            LowerCallable(callable, scope);
    }

    private void LowerCallable(BoundCallable callable, BindingScope scope)
    {
        ResetLocals();
        var key = GetCallableKey(callable);
        logger.Debug(Phase, $"Lowering callable '{key}'");

        if (!scope.TryGetResolvedBody(callable, out var body) || body is null)
        {
            logger.Warning(Phase, $"No resolved body for '{key}' - skipping.");
            return;
        }

        _localSlots["this"] = AllocateSlot();
        foreach (var param in callable.Parameters)
            _localSlots[param.Name] = AllocateSlot();

        var instructions = new List<ushort>();
        var statements = body switch
        {
            BoundMethodBody m => m.Statements,
            BoundConstructorBody c => c.Statements,
            BoundPropertyGetterBody g => g.Statements,
            BoundPropertySetterBody s => s.Statements,
            _ => null
        };

        if (statements is null)
        {
            logger.Warning(Phase, $"Unknown body type for '{key}' - skipping.");
            return;
        }

        foreach (var stmt in statements)
            LowerStatement(stmt, instructions);
        
        if (instructions.Count == 0 || instructions[^1] != (ushort)OpCode.RET)
            instructions.Add((ushort)OpCode.RET);

        if (!_methodIndices.TryGetValue(key, out var methodIndex))
        {
            logger.Error(Phase, $"No method index for '{key}'");
            return;
        }
        
        var lowered = new LoweredMethod(callable.Name, [..instructions], _localCount, (ushort)callable.Parameters.Count);
        _methods.Replace(methodIndex, lowered);
    }

    #endregion

    #region Utilities

    private static string GetCallableKey(BoundCallable callable)
    {
        var returnType = callable switch
        {
            BoundMethod m => m.ReturnType.DisplayName,
            BoundConstructor c => c.ParentType.DisplayName,
            BoundPropertyGetter g => g.ReturnType.DisplayName,
            BoundPropertySetter s => s.ValueType.DisplayName,
            _ => "unknown"
        };

        var parameters = string.Join(", ", callable.Parameters.Select(p => p.Type.DisplayName));
        return $"{returnType} {callable.Name}({parameters})";
    }

    private ushort GetOrAddStdLibImport()
    {
        const string stdlibName = "stdlib";
        var existing = _imports.All.FirstOrDefault(i => i.ModuleName == stdlibName);
        if (existing is not null)
            return (ushort)_imports.All.ToList().IndexOf(existing);
        return _imports.Add(stdlibName, StdLib.ModuleIndex);
    }

    #endregion
}