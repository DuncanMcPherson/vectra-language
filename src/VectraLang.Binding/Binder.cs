using VectraLang.Ast.AstNodes;
using VectraLang.Ast.Tokens;
using VectraLang.Binding;
using VectraLang.Binding.Nodes;
using VectraLang.Binding.Scope;

namespace VectraLang.Core;

public class Binder
{
    private readonly BindingScope _scope = new();
    private readonly List<string> _errors = [];
    private BoundType? _currentReturnType;
    private LocalScope _localScope = new();

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
        // TODO: evaluate.
        // in the future, we will have multiple files and will need to allow multiple files in the same space
        if (!_scope.TryRegisterSpace(space))
            _errors.Add(CreateError($"Space '{space.Name.Lexeme}' already declared.", space.Name.Location));
        foreach (var decl in space.Declarations.Where(decl => !_scope.TryRegisterType(decl)))
            _errors.Add(CreateError($"Type '{decl.Name.Lexeme}' already declared.", decl.Name.Location));
        foreach (var child in space.Children)
            PassOne(child);
    }

    private List<string> PassTwo(List<EnterDecl> enters)
    {
        var resolved = new List<string>();
        foreach (var enter in enters)
        {
            if (_scope.TryResolveSpace(enter.QualifiedName, out _))
                resolved.Add(enter.QualifiedName);
            else
                _errors.Add(CreateError($"Unresolved space: '{enter.QualifiedName}'.", enter.Location));
        }

        return resolved;
    }

    private BoundSpace PassThree(SpaceDecl space)
    {
        var boundDecls = new List<BoundTypeDecl>();
        foreach (var decl in space.Declarations)
        {
            BoundTypeDecl? bound = decl switch
            {
                ClassDecl c => BindClass(c),
                InterfaceDecl i => BindInterface(i),
                EnumDecl e => BindEnum(e),
                _ => null
            };

            if (bound is not null)
                boundDecls.Add(bound);
        }

        var boundChildren = space.Children.Select(PassThree).ToList();
        return new BoundSpace(space.Name.Lexeme, boundDecls, boundChildren);
    }

    private BoundClass BindClass(ClassDecl decl)
    {
        return new BoundClass(
            decl.GetFullName(),
            decl.Fields.Select(BindField).ToList(),
            decl.Properties.Select(BindProperty).ToList(),
            decl.Methods.Select(BindMethod).ToList(),
            decl.Constructors.Select(BindConstructor).ToList(),
            decl);
    }

    private BoundInterface BindInterface(InterfaceDecl decl)
    {
        return new(
            decl.GetFullName(),
            decl.Methods.Select(BindMethodSignature).ToList(),
            decl);
    }

    private BoundEnum BindEnum(EnumDecl decl)
    {
        return new(
            decl.GetFullName(),
            decl.Fields.Select(BindField).ToList(),
            decl.Methods.Select(BindMethod).ToList(),
            decl);
    }

    private BoundField BindField(FieldDecl decl)
    {
        return new(
            decl.Name.Lexeme,
            ResolveTypeNode(decl.Type),
            decl);
    }

    private BoundProperty BindProperty(PropertyDecl decl)
    {
        return new(
            decl.Name.Lexeme,
            ResolveTypeNode(decl.Type),
            decl);
    }

    private BoundParameter BindParameter(ParameterNode param)
    {
        return new(param.Name.Lexeme, ResolveTypeNode(param.Type), param);
    }

    private BoundMethod BindMethod(MethodDecl decl)
    {
        var ret = new BoundMethod(
            decl.Name.Lexeme,
            ResolveTypeNode(decl.ReturnType),
            decl.Parameters.Select(BindParameter).ToList(),
            decl);
        _scope.RegisterPendingBody(ret, decl.Body);
        return ret;
    }

    private BoundMethodSignature BindMethodSignature(MethodSignatureDecl decl) =>
        new(
            decl.Name.Lexeme,
            ResolveTypeNode(decl.ReturnType),
            decl.Parameters.Select(BindParameter).ToList(),
            decl);

    private BoundConstructor BindConstructor(ConstructorDecl decl)
    {
        var ret = new BoundConstructor(
            decl.Name.Lexeme,
            decl.Parameters.Select(BindParameter).ToList(),
            decl);
        _scope.RegisterPendingBody(ret, decl.Body);
        return ret;
    }

    private void PassFour()
    {
        foreach (var (callable, stmt) in _scope.GetPendingBodies())
        {
            BoundCallableBody? resolved = callable switch
            {
                BoundMethod m => new BoundMethodBody(BindBodyStatement(stmt, m.ReturnType), m.Source),
                BoundConstructor c => new BoundConstructorBody(BindBodyStatement(stmt), c.Source),
                _ => null
            };
            
            if (resolved is not null)
                _scope.RegisterResolvedBody(callable, resolved);
        }
    }

    private List<BoundStmt> BindBodyStatement(BlockStmt stmt, BoundType? returnType = null)
    {
        if (returnType is not null)
            _currentReturnType = returnType;
        
        var previous = _localScope;
        _localScope = _localScope.CreateChildScope();
        try
        {
            var ret = new List<BoundStmt>();
            foreach (var s in stmt.Statements)
                ret.Add(BindStatement(s));

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
        _errors.Add(CreateError(message, location));
        return new BoundErrorStmt();
    }

    private BoundBlockStmt BindBlock(BlockStmt block)
    {
        var previous = _localScope;
        _localScope = _localScope.CreateChildScope();
        try
        {
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
        var boundType = ResolveTypeNode(decl.Type);
        var initializer = decl.Initializer is not null ? BindExpr(decl.Initializer) : null;
        // TODO: should we swap the bound type if the initializer is not null?
        if (!_localScope.TryDeclare(decl.Name.Lexeme, boundType))
            _errors.Add(CreateError($"Variable with name '{decl.Name.Lexeme}' already declared in this scope.", decl.Location));
        
        return new BoundVarDeclStmt(decl.Name.Lexeme, boundType, initializer);
    }

    private BoundIfStmt BindIf(IfStmt stmt)
    {
        var condition = BindExpr(stmt.Condition);
        var thenBranch = BindStatement(stmt.ThenBranch);
        var elseBranch = stmt.ElseBranch is not null ? BindStatement(stmt.ElseBranch) : null;
        return new BoundIfStmt(condition, thenBranch, elseBranch);
    }

    private BoundForStmt BindFor(ForStmt stmt)
    {
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
        var condition = BindExpr(stmt.Condition);
        var body = BindStatement(stmt.Body);
        return new BoundWhileStmt(condition, body);
    }

    private BoundReturnStmt BindReturn(ReturnStmt stmt)
    {
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
        var inner = BindExpr(g.Inner);
        return new BoundGroupingExpr(inner, inner.Type);
    }

    private BoundVariableExpr BindVariable(VariableExpr e)
    {
        if (_localScope.TryResolve(e.Name.Lexeme, out var type) && type is not null)
            return new BoundVariableExpr(e.Name.Lexeme, type);
        _errors.Add(CreateError($"Unresolved variable: '{e.Name.Lexeme}'.", e.Location));
        return new BoundVariableExpr(e.Name.Lexeme, new BoundErrorType(e.Name.Lexeme));
    }

    private BoundAssignExpr BindAssign(AssignExpr e)
    {
        var target = BindExpr(e.Target);
        var value = BindExpr(e.Value);
        return new BoundAssignExpr(target, value, target.Type);
    }

    private BoundBinaryExpr BindBinary(BinaryExpr e)
    {
        var left = BindExpr(e.Left);
        var right = BindExpr(e.Right);
        // Type resolution will be handled in the analysis phase
        return new BoundBinaryExpr(left, e.Operator, right, left.Type);
    }

    private BoundUnaryExpr BindUnary(UnaryExpr e)
    {
        var operand = BindExpr(e.Right);
        return new BoundUnaryExpr(e.Operator, operand, operand.Type);
    }

    private BoundCallExpr BindCall(CallExpr e)
    {
        var callee = BindExpr(e.Callee);
        var args = e.Arguments.Select(BindExpr).ToList();
        return new BoundCallExpr(callee, args, new BoundInferredType());
    }

    private BoundGetExpr BindGet(GetExpr e)
    {
        var obj = BindExpr(e.Object);
        return new BoundGetExpr(obj, e.Name.Lexeme, new BoundInferredType());
    }

    private BoundNewExpr BindNew(NewExpr e)
    {
        var targetType = ResolveUserType(e.TypeName.Lexeme, e.Location);
        var args = e.Arguments.Select(BindExpr).ToList();
        return new BoundNewExpr(targetType, args, new BoundInferredType());
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
        if (_scope.TryResolveType(name, out var decl) && decl is not null)
            return new BoundUserDefinedType(name, decl);
        
        _errors.Add(CreateError($"Unresolved type: '{name}'.", location));
        return new BoundErrorType(name);
    }

    private BoundType ResolveGenericType(GenericTypeNode generic)
    {
        var typeArgs = generic.TypeArguments.Select(ResolveTypeNode).ToList();
        if (_scope.TryResolveType(generic.TypeToken.Lexeme, out var decl) && decl is not null)
            return new BoundGenericType(generic.TypeToken.Lexeme, decl, typeArgs);
        
        _errors.Add(CreateError($"Unresolved type: '{generic.TypeToken.Lexeme}'.", generic.Location));
        return new BoundErrorType(generic.TypeToken.Lexeme);
    }

    private static bool IsBuiltInType(string name) => name switch
    {
        "int" or "float" or "double" or "bool" or "string" or "void" => true,
        _ => false
    };
    
    private static string CreateError(string message, TokenLocation location) => $"[[line {location.StartLine}:{location.StartColumn}]] Error: {message}\n\tIn file '{location.FileName}'";
}