using VectraLang.Ast.AstNodes;
using VectraLang.Binding;
using VectraLang.Binding.Nodes;
using VectraLang.Binding.Scope;
using VectraLang.Core.Diagnostics;
using VectraLang.ModuleLoader;

namespace VectraLang.Core;

public class Binder
{
    private readonly BindingScope _scope;
    private readonly List<string> _errors = [];
    private readonly IVectraLogger _logger;
    private BoundType? _currentReturnType;
    private LocalScope _localScope = new();

    public Binder(IVectraLogger logger, BindingScope? scope = null)
    {
        _logger = logger;
        _scope = scope ?? new BindingScope();
    }

    public BindingResult Bind(MergedPackage package)
    {
        var sharedScope = new BindingScope();
        var boundModules = new List<BoundModule>(package.Modules.Count);
        var allErrors = new List<string>();

        foreach (var result in from module in package.Modules let binder = new Binder(_logger, sharedScope) select binder.Bind(module))
        {
            allErrors.AddRange(result.Errors);
            if (result.BoundRoot is BoundModule bm)
                boundModules.Add(bm);
        }
        
        var boundPackage = new BoundPackage(package.PackageName, package.Version, boundModules);
        return new BindingResult(boundPackage, sharedScope, allErrors);
    }

    public BindingResult Bind(MergedModule module)
    {
        if (module.SpaceDecls.Count == 0)
        {
            _logger.Warning("Bind", $"Module '{module.ModuleName}' has no spaces. Skipping.");
            var bound = new BoundModule(module.ModuleName, [], module.IsExecutable, []);
            return new BindingResult(bound, _scope, _errors);
        }
        
        foreach (var space in module.SpaceDecls)
        {
            PassOne(space);
        }
        
        // TODO: Preserve enter declarations from each file for cross-module binding
        // Currently discarded during the merge
        var resolvedImports = PassTwo(module.EnterDeclarations);
        
        var boundSpaces = module.SpaceDecls.Select(PassThree).ToList();
        
        PassFour();
        
        var boundModule = new BoundModule(module.ModuleName, boundSpaces, module.IsExecutable, resolvedImports);
        
        return new BindingResult(boundModule, _scope, _errors);
    }

    public BindingResult Bind(VectraFile file)
    {
        // Register Spaces and Types
        PassOne(file.Space);
        // Resolve enters
        var resolvedImports = PassTwo(file.EnterDeclarations);
        // Resolve members (Fields, Properties, Methods, Constructors)
        var boundSpace = PassThree(file.Space);
        // Resolve bodies
        PassFour();

        var boundFile = new BoundFile(boundSpace, resolvedImports);
        return new BindingResult(boundFile, _scope, _errors);
    }

    private void PassOne(SpaceDecl space)
    {
        // in the future, we will have multiple files and will need to allow multiple files in the same space
        _logger.Debug("Bind:1", $"Registering space '{space.Name.Lexeme}'");
        if (!_scope.TryRegisterSpace(space, out var alreadyExists) && !alreadyExists)
            _logger.Error("Bind:1", $"Space '{space.Name.Lexeme}' already declared.", space.Name.Location);
        foreach (var decl in space.Declarations.Where(decl => !_scope.TryRegisterType(decl)))
            _logger.Error("Bind:1", $"Type '{decl.Name.Lexeme}' already declared.", decl.Name.Location);
        _logger.Debug("Bind:1", $"Registered space '{space.Name.Lexeme}' with {space.Declarations.Count} types and {space.Children.Count} spaces.");
        foreach (var child in space.Children)
            PassOne(child);
    }

    private List<string> PassTwo(List<EnterDecl> enters)
    {
        var resolved = new List<string>(enters.Count);
        foreach (var enter in enters)
        {
            _logger.Debug("Bind:2", $"Resolving enter '{enter.QualifiedName}'");
            if (_scope.TryResolveSpace(enter.QualifiedName, out _))
                resolved.Add(enter.QualifiedName);
            else
            {
                _logger.Error("Bind:2", $"Unable to locate space '{enter.QualifiedName}'", enter.Location);                
            }
        }

        return resolved;
    }

    private BoundSpace PassThree(SpaceDecl space)
    {
        var boundDecls = new List<BoundTypeDecl>(space.Declarations.Count);
        foreach (var decl in space.Declarations)
        {
            _logger.Debug("Bind:3", $"Binding type '{decl.Name.Lexeme}'");
            BoundTypeDecl? bound = decl switch
            {
                ClassDecl c => BindClass(c),
                InterfaceDecl i => BindInterface(i),
                EnumDecl e => BindEnum(e),
                _ => null
            };

            if (bound is not null)
            {
                _logger.Debug("Bind:3", $"Registering bound type: '{bound.QualifiedName}'");
                _scope.RegisterBoundType(bound);
                boundDecls.Add(bound);
            }
        }

        var boundChildren = space.Children.Select(PassThree).ToList();
        _logger.Debug("Bind:3", $"Registered {boundDecls.Count} types and {boundChildren.Count} spaces.");
        return new BoundSpace(space.Name.Lexeme, boundDecls, boundChildren);
    }

    private BoundClass BindClass(ClassDecl decl)
    {
        _logger.Debug("Bind:3", $"Binding class '{decl.Name.Lexeme}'");
        var classType = new BoundUserDefinedType(decl.GetFullName(), decl);
        return new BoundClass(
            decl.GetFullName(),
            decl.Fields.Select(BindField).ToList(),
            decl.Properties.Select(p => BindProperty(p, classType)).ToList(),
            decl.Methods.Select(m => BindMethod(m, classType)).ToList(),
            decl.Constructors.Select(m => BindConstructor(m, classType)).ToList(),
            decl);
    }

    private BoundInterface BindInterface(InterfaceDecl decl)
    {
        _logger.Debug("Bind:3", $"Binding interface '{decl.Name.Lexeme}'");
        return new(
            decl.GetFullName(),
            decl.Methods.Select(BindMethodSignature).ToList(),
            decl);
    }

    private BoundEnum BindEnum(EnumDecl decl)
    {
        _logger.Debug("Bind:3", $"Binding enum '{decl.Name.Lexeme}'");
        var enumType = new BoundUserDefinedType(decl.GetFullName(), decl);
        var fields = decl.Fields.Select(BindField).ToList();
        var methods = decl.Methods.Select(m => BindMethod(m, enumType)).ToList();
        var variants = decl.Variants.Select(v => BindEnumVariant(v, decl, enumType)).ToList();

        return new(decl.GetFullName(), fields, methods, variants, decl);
    }

    private BoundEnumVariant BindEnumVariant(EnumVariantNode variant, EnumDecl parentEnum, BoundType enumType)
    {
        // Arguments are bound against the enum's field types in order
        var boundArgs = variant.Arguments.Select(BindExpr).ToList();
        // Overrides are full methods, same treatment as class methods
        var boundOverrides = variant.Overrides.Select(o => BindMethod(o, enumType)).ToList();
        
        return new(variant.Name.Lexeme, boundArgs, boundOverrides, variant);
    }

    private BoundField BindField(FieldDecl decl)
    {
        _logger.Debug("Bind:3", $"Binding field '{decl.Name.Lexeme}'");
        var initializer = decl.Initializer is not null ? BindExpr(decl.Initializer) : null;
        return new(
            decl.Name.Lexeme,
            ResolveTypeNode(decl.Type),
            initializer,
            decl);
    }

    private BoundProperty BindProperty(PropertyDecl decl, BoundType classType)
    {
        _logger.Debug("Bind:3", $"Binding property '{decl.Name.Lexeme}'");
        var propertyType = ResolveTypeNode(decl.Type);
        BoundPropertyGetter? getter = null;
        BoundPropertySetter? setter = null;

        if (decl.Getter is not null)
        {
            getter = new BoundPropertyGetter(decl.Name.Lexeme, propertyType, classType, decl);
            _scope.RegisterPendingBody(getter, decl.Getter!);
        }

        if (decl.Setter is not null)
        {
            setter = new BoundPropertySetter(decl.Name.Lexeme, propertyType, classType, decl);
            _scope.RegisterPendingBody(setter, decl.Setter!);
        }
        
        return new BoundProperty(decl.Name.Lexeme, propertyType, decl, getter, setter);   
    }

    private BoundParameter BindParameter(ParameterNode param)
    {
        _logger.Debug("Bind:3", $"Binding parameter '{param.Name.Lexeme}'");
        return new(param.Name.Lexeme, ResolveTypeNode(param.Type), param);
    }

    private BoundMethod BindMethod(MethodDecl decl, BoundType classType)
    {
        _logger.Debug("Bind:3", $"Binding method '{decl.Name.Lexeme}'");
        var ret = new BoundMethod(
            decl.Name.Lexeme,
            ResolveTypeNode(decl.ReturnType),
            decl.Parameters.Select(BindParameter).ToList(),
            classType,
            decl);
        _scope.RegisterPendingBody(ret, decl.Body);
        _logger.Debug("Bind:3", $"Registered method body '{decl.Name.Lexeme}' for resolution in Pass 4.");
        return ret;
    }

    private BoundMethodSignature BindMethodSignature(MethodSignatureDecl decl)
    {
        _logger.Debug("Bind:3", $"Binding method signature '{decl.Name.Lexeme}'");
        return new(
            decl.Name.Lexeme,
            ResolveTypeNode(decl.ReturnType),
            decl.Parameters.Select(BindParameter).ToList(),
            decl);
    }

    private BoundConstructor BindConstructor(ConstructorDecl decl, BoundType classType)
    {
        _logger.Debug("Bind:3", $"Binding constructor '{decl.Name.Lexeme}'");
        var ret = new BoundConstructor(
            decl.Name.Lexeme,
            decl.Parameters.Select(BindParameter).ToList(),
            classType,
            decl);
        _scope.RegisterPendingBody(ret, decl.Body);
        _logger.Debug("Bind:3", $"Registered constructor body '{decl.Name.Lexeme}' for resolution in Pass 4.");       
        return ret;
    }

    private void PassFour()
    {
        _logger.Debug("Bind:4", "Resolving bodies.");
        while (_scope.GetPendingBodiesCount() > 0)
        {
            var (callable, stmt) = _scope.DequeuePendingBody();
            _logger.Debug("Bind:4", $"Resolving body for '{callable.Name}'");
            
            var resolved = callable switch
            {
                BoundMethod m => new BoundMethodBody(BindBodyStatement(stmt, m.Parameters, m.ParentType, m.ReturnType), m.Source),
                BoundConstructor c => new BoundConstructorBody(BindBodyStatement(stmt, c.Parameters, c.ParentType), c.Source),
                BoundPropertyGetter g => new BoundPropertyGetterBody(BindBodyStatement(stmt, g.Parameters, g.ParentType, g.ReturnType), g.Source),
                BoundPropertySetter s => new BoundPropertySetterBody(BindBodyStatement(stmt, s.Parameters, s.ParentType), s.Source),
                _ => HandleUnknown(callable)
            };
            
            if (resolved is not null)
                _scope.RegisterResolvedBody(callable, resolved);
        }
    }

    private BoundCallableBody? HandleUnknown(BoundCallable callable)
    {
        var callableType = callable.GetType().Name;
        _logger.Error("Bind:4", $"Unknown callable type '{callableType}'");
        return null;
    }

    private List<BoundStmt> BindBodyStatement(BlockStmt stmt, List<BoundParameter> parameters, BoundType parentType, BoundType? returnType = null)
    {
        if (returnType is not null)
            _currentReturnType = returnType;
        
        var previous = _localScope;
        _localScope = _localScope.CreateChildScope();
        try
        {
            _localScope.TryDeclare("this", parentType);
            foreach (var p in parameters.Where(p => !_localScope.TryDeclare(p.Name, p.Type)))
            {
                _logger.Error("Bind:4", $"Variable with name '{p.Name}' already declared in this scope.", p.Source.Location);
            }
            var ret = new List<BoundStmt>(stmt.Statements.Count);
            foreach (var s in stmt.Statements)
                ret.Add(BindStatement(s));

            _logger.Debug("Bind:4", "Bound body successfully");
            return ret;
        }
        finally
        {
            _localScope = previous;
            _currentReturnType = null;
        }
    }

    private BoundStmt BindStatement(Stmt s) => s switch
    {
        BlockStmt b => BindBlock(b),
        VarDeclStmt v => BindVarDecl(v),
        ExprStmt e => new BoundExprStmt(BindExpr(e.Expression)),
        IfStmt i => BindIf(i),
        WhileStmt w => BindWhile(w),
        ForStmt f => BindFor(f),
        ReturnStmt r => BindReturn(r),
        BreakStmt => new BoundBreakStmt(),
        ContinueStmt => new BoundContinueStmt(),
        _ => CreateErrorStmt(s.Location, "Unknown statement type.")
    };

    private BoundErrorStmt CreateErrorStmt(TokenLocation location, string message)
    {
        _logger.Error("Bind:4", message, location);
        return new BoundErrorStmt();
    }

    private BoundBlockStmt BindBlock(BlockStmt block)
    {
        var previous = _localScope;
        _localScope = _localScope.CreateChildScope();
        try
        {
            _logger.Debug("Bind:4", "Binding block statements");
            var boundStmts = block.Statements.Select(BindStatement).ToList();
            return new BoundBlockStmt(boundStmts);
        }
        finally
        {
            _localScope = previous;
        }
    }

    private BoundVarDeclStmt BindVarDecl(VarDeclStmt decl)
    {
        _logger.Debug("Bind:4", $"Binding variable declaration '{decl.Name.Lexeme}'", decl.Location);
        var boundType = ResolveTypeNode(decl.Type);
        var initializer = decl.Initializer is not null ? BindExpr(decl.Initializer) : null;
        if (boundType is BoundInferredType && initializer is not null)
            boundType = initializer.Type;
        if (!_localScope.TryDeclare(decl.Name.Lexeme, boundType))
            _logger.Error("Bind:4", $"Variable with name '{decl.Name.Lexeme}' already declared in this scope.", decl.Location);
        
        return new BoundVarDeclStmt(decl.Name.Lexeme, boundType, initializer);
    }

    private BoundIfStmt BindIf(IfStmt stmt)
    {
        _logger.Debug("Bind:4", "Binding if statement", stmt.Location);
        var condition = BindExpr(stmt.Condition);
        var thenBranch = BindStatement(stmt.ThenBranch);
        var elseBranch = stmt.ElseBranch is not null ? BindStatement(stmt.ElseBranch) : null;
        return new BoundIfStmt(condition, thenBranch, elseBranch);
    }

    private BoundForStmt BindFor(ForStmt stmt)
    {
        _logger.Debug("Bind:4", "Binding for statement", stmt.Location);
        var previous = _localScope;
        _localScope = _localScope.CreateChildScope();
        try
        {
            var init = stmt.Initializer is not null ? BindStatement(stmt.Initializer) : null;
            var condition = stmt.Condition is not null ? BindExpr(stmt.Condition) : null;
            var increment = stmt.Increment is not null ? BindExpr(stmt.Increment) : null;
            var body = BindStatement(stmt.Body);
            return new BoundForStmt(init, condition, increment, body);
        }
        finally
        {
            _localScope = previous;
        }
    }

    private BoundWhileStmt BindWhile(WhileStmt stmt)
    {
        _logger.Debug("Bind:4", "Binding while statement", stmt.Location);
        var condition = BindExpr(stmt.Condition);
        var body = BindStatement(stmt.Body);
        return new BoundWhileStmt(condition, body);
    }

    private BoundReturnStmt BindReturn(ReturnStmt stmt)
    {
        _logger.Debug("Bind:4", "Binding return statement", stmt.Location);
        var value = stmt.Value is not null ? BindExpr(stmt.Value) : null;
        return new BoundReturnStmt(value, _currentReturnType);
    }

    private BoundExpr BindExpr(Expr expr)
    {
        return expr switch
        {
            IntegerLiteralExpr i => new BoundIntegerLiteralExpr(i.Value, new BoundPrimitiveType("int")),
            FloatLiteralExpr f => new BoundFloatLiteralExpr(f.Value, new BoundPrimitiveType("float")),
            StringLiteralExpr s => new BoundStringLiteralExpr(s.Value, new BoundPrimitiveType("string")),
            BoolLiteralExpr b => new BoundBoolLiteralExpr(b.Value, new BoundPrimitiveType("bool")),
            NullLiteralExpr => new BoundNullLiteralExpr(new BoundPrimitiveType("null")),
            VariableExpr v => BindVariable(v),
            AssignExpr a => BindAssign(a),
            BinaryExpr b => BindBinary(b),
            UnaryExpr u => BindUnary(u),
            GroupingExpr g => BindGrouping(g),
            CallExpr c => BindCall(c),
            GetExpr g => BindGet(g),
            NewExpr n => BindNew(n),
            _ => new BoundErrorExpr(new BoundErrorType("unknown"))
        };
    }

    private BoundGroupingExpr BindGrouping(GroupingExpr g)
    {
        _logger.Debug("Bind:4", "Binding grouping expression", g.Location);
        var inner = BindExpr(g.Inner);
        return new BoundGroupingExpr(inner, inner.Type);
    }

    private BoundVariableExpr BindVariable(VariableExpr e)
    {
        _logger.Debug("Bind:4", $"Binding variable expression '{e.Name.Lexeme}'", e.Location);
        if (_localScope.TryResolve(e.Name.Lexeme, out var type) && type is not null)
            return new BoundVariableExpr(e.Name.Lexeme, type);

        _logger.Debug("Bind:4", $"Binding variable expression '{e.Name.Lexeme}' as global", e.Location);
        if (_scope.TryResolveGlobalFunction(e.Name.Lexeme, out var fn) && fn is not null)
            return new BoundVariableExpr(e.Name.Lexeme, fn.ReturnType);
        
        _logger.Error("Bind:4", $"Unresolved variable: '{e.Name.Lexeme}'.", e.Location);
        return new BoundVariableExpr(e.Name.Lexeme, new BoundErrorType(e.Name.Lexeme));
    }

    private BoundAssignExpr BindAssign(AssignExpr e)
    {
        _logger.Debug("Bind:4", $"Binding assignment expression", e.Location);
        var target = BindExpr(e.Target);
        var value = BindExpr(e.Value);
        return new BoundAssignExpr(target, value, target.Type);
    }

    private BoundBinaryExpr BindBinary(BinaryExpr e)
    {
        _logger.Debug("Bind:4", $"Binding binary expression '{e.Operator.Lexeme}'", e.Location);
        var left = BindExpr(e.Left);
        var right = BindExpr(e.Right);
        // Type resolution will be handled in the analysis phase
        return new BoundBinaryExpr(left, e.Operator, right, left.Type);
    }

    private BoundUnaryExpr BindUnary(UnaryExpr e)
    {
        _logger.Debug("Bind:4", $"Binding unary expression '{e.Operator.Lexeme}'", e.Location);
        var operand = BindExpr(e.Right);
        return new BoundUnaryExpr(e.Operator, operand, operand.Type);
    }

    private BoundCallExpr BindCall(CallExpr e)
    {
        _logger.Debug("Bind:4", $"Binding call expression", e.Location);
        var callee = BindExpr(e.Callee);
        var args = e.Arguments.Select(BindExpr).ToList();
        return new BoundCallExpr(callee, args, callee.Type);
    }

    private BoundGetExpr BindGet(GetExpr e)
    {
        _logger.Debug("Bind:4", $"Binding get expression", e.Location);
        var obj = BindExpr(e.Object);
        _logger.Debug("Bind:4", $"Bound object expression, resolving member '{e.Name.Lexeme}'", e.Location);
        if (_scope.TryResolveMember(obj.Type, e.Name.Lexeme, out var memberType) && memberType is not null)
            return new BoundGetExpr(obj, e.Name.Lexeme, memberType);
        
        _logger.Error("Bind:4", $"'{e.Name.Lexeme}' is not a member of '{obj.Type}'", e.Location);
        return new BoundGetExpr(obj, e.Name.Lexeme, new BoundErrorType(e.Name.Lexeme));
    }

    private BoundNewExpr BindNew(NewExpr e)
    {
        _logger.Debug("Bind:4", $"Binding new expression", e.Location);
        var targetType = ResolveUserType(e.TypeName.Lexeme, e.Location);
        var args = e.Arguments.Select(BindExpr).ToList();
        return new BoundNewExpr(targetType, args, targetType);
    }

    private BoundType ResolveTypeNode(TypeNode typeNode) => typeNode switch
    {
        PrimitiveTypeNode p => IsBuiltInType(p.TypeToken.Lexeme)
            ? new BoundPrimitiveType(p.TypeToken.Lexeme)
            : ResolveUserType(p.TypeToken.Lexeme, p.Location),
        GenericTypeNode g => ResolveGenericType(g),
        InferredTypeNode => new BoundInferredType(),
        _ => new BoundErrorType("unknown")
    };

    private BoundType ResolveUserType(string name, TokenLocation location)
    {
        _logger.Debug("Bind:4", $"Resolving user type '{name}'", location);
        if (_scope.TryResolveType(name, out var decl) && decl is not null)
            return new BoundUserDefinedType(name, decl);
        
        _logger.Error("Bind:4", $"Unresolved type: '{name}'.", location);
        return new BoundErrorType(name);
    }

    private BoundType ResolveGenericType(GenericTypeNode generic)
    {
        _logger.Debug("Bind:4", $"Resolving generic type '{generic.TypeToken.Lexeme}'", generic.Location);
        var typeArgs = generic.TypeArguments.Select(ResolveTypeNode).ToList();
        if (_scope.TryResolveType(generic.TypeToken.Lexeme, out var decl) && decl is not null)
            return new BoundGenericType(generic.TypeToken.Lexeme, decl, typeArgs);
        
        _logger.Error("Bind:4", $"Unresolved type: '{generic.TypeToken.Lexeme}'.", generic.Location);
        return new BoundErrorType(generic.TypeToken.Lexeme);
    }

    private static bool IsBuiltInType(string name) => name switch
    {
        "int" or "float" or "double" or "bool" or "string" or "void" => true,
        _ => false
    };
}