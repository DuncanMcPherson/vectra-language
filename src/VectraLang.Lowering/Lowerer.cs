using VectraLang.Ast.AstNodes;
using VectraLang.Ast.Tokens;
using VectraLang.Binding;
using VectraLang.Binding.Nodes;
using VectraLang.Binding.Scope;
using VectraLang.Bytecode;
using VectraLang.Core.Diagnostics;
using VectraLang.Lowering.Helpers;

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
    private readonly Dictionary<string, BoundClass> _boundClasses = new(); 
    private ushort _localCount;
    private readonly Dictionary<string, ushort> _localSlots = new();
    private readonly Stack<LoopContext> _loopContexts = new();

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
        _boundClasses.Add(cls.QualifiedName, cls);
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

    private void LowerStatement(BoundStmt stmt, List<ushort> instructions)
    {
        switch (stmt)
        {
            case BoundBlockStmt block:
                LowerBlock(block, instructions);
                break;
            case BoundVarDeclStmt varDeclStmt:
                LowerVarDecl(varDeclStmt, instructions);
                break;
            case BoundExprStmt exprStmt:
                LowerExpr(exprStmt.Expression, instructions);
                instructions.Add((ushort)OpCode.POP);
                break;
            case BoundIfStmt ifStmt:
                LowerIf(ifStmt, instructions);
                break;
            case BoundWhileStmt whileStmt:
                LowerWhile(whileStmt, instructions);
                break;
            case BoundForStmt forStmt:
                LowerFor(forStmt, instructions);
                break;
            case BoundReturnStmt returnStmt:
                LowerReturn(returnStmt, instructions);
                break;
            case BoundBreakStmt:
                LowerBreak(instructions);
                break;
            case BoundContinueStmt:
                LowerContinue(instructions);
                break;
            case BoundErrorStmt:
                // Already reported
                break;
            default:
                logger.Warning(Phase, $"Unexpected stmt type: {stmt.GetType().Name}");
                break;
        }
    }

    private void LowerBlock(BoundBlockStmt block, List<ushort> instructions)
    {
        foreach (var stmt in block.Statements)
            LowerStatement(stmt, instructions);
    }

    private void LowerVarDecl(BoundVarDeclStmt decl, List<ushort> instructions)
    {
        var slot = AllocateSlot();
        _localSlots[decl.Name] = slot;

        if (decl.Initializer is not null)
        {
            LowerExpr(decl.Initializer, instructions);
        }
        else
        {
            instructions.Add((ushort)OpCode.PUSH_NULL);
        }
        instructions.Add((ushort)OpCode.STORE_LOCAL);
        instructions.Add(slot);
    }

    private void LowerIf(BoundIfStmt ifStmt, List<ushort> instructions)
    {
        LowerExpr(ifStmt.Condition, instructions);
        var thenPatch = new BackpatchList();
        instructions.Add((ushort)OpCode.JMP_FALSE);
        thenPatch.Add(instructions.Count);
        instructions.Add(0); // placeholder
        
        LowerStatement(ifStmt.ThenBranch, instructions);

        if (ifStmt.ElseBranch is not null)
        {
            var elsePatch = new BackpatchList();
            instructions.Add((ushort)OpCode.JMP);
            elsePatch.Add(instructions.Count);
            instructions.Add(0); // placeholder
            
            thenPatch.PatchAll(instructions);
            
            LowerStatement(ifStmt.ElseBranch, instructions);
            
            elsePatch.PatchAll(instructions);
        }
        else
        {
            thenPatch.PatchAll(instructions);
        }
    }

    private void LowerFor(BoundForStmt stmt, List<ushort> instructions)
    {
        if (stmt.Initializer is not null)
            LowerStatement(stmt.Initializer, instructions);
        
        var conditionTarget = (ushort)instructions.Count;
        
        var exitPatch = new BackpatchList();
        if (stmt.Condition is not null)
        {
            LowerExpr(stmt.Condition, instructions);
            instructions.Add((ushort)OpCode.JMP_FALSE);
            exitPatch.Add(instructions.Count);
            instructions.Add(0);
        }
        
        var loopCtx = new LoopContext();
        _loopContexts.Push(loopCtx);
        
        LowerStatement(stmt.Body, instructions);
        
        var incrementTarget = (ushort)instructions.Count;
        loopCtx.SetContinueTarget(incrementTarget);
        loopCtx.ContinueJumps.PatchAll(instructions);

        if (stmt.Increment is not null)
        {
            LowerExpr(stmt.Increment, instructions);
            instructions.Add((ushort)OpCode.POP);
        }
        
        instructions.Add((ushort)OpCode.JMP);
        instructions.Add(conditionTarget);
        
        exitPatch.PatchAll(instructions);
        loopCtx.BreakJumps.PatchAll(instructions);
        
        _loopContexts.Pop();
    }

    private void LowerReturn(BoundReturnStmt stmt, List<ushort> instructions)
    {
        if (stmt.Value is not null)
        {
            LowerExpr(stmt.Value, instructions);
            instructions.Add((ushort)OpCode.RET_VAL);
        }
        else
        {
            instructions.Add((ushort)OpCode.RET);
        }
    }

    private void LowerContinue(List<ushort> instructions)
    {
        if (_loopContexts.Count == 0)
        {
            logger.Error(Phase, "Continue outside of loop");
            return;
        }

        var ctx = _loopContexts.Peek();
        instructions.Add((ushort)OpCode.JMP);

        ctx.ContinueJumps.Add(instructions.Count);
        instructions.Add(0);
    }

    private void LowerBreak(List<ushort> instructions)
    {
        if (_loopContexts.Count == 0)
        {
            logger.Error(Phase, "Break outside of loop");
            return;
        }
        
        var ctx = _loopContexts.Peek();
        instructions.Add((ushort)OpCode.JMP);
        
        ctx.BreakJumps.Add(instructions.Count);
        instructions.Add(0);
    }

    private void LowerWhile(BoundWhileStmt stmt, List<ushort> instructions)
    {
        var continueTarget = (ushort)instructions.Count;
        var loopCtx = new LoopContext();
        loopCtx.SetContinueTarget(continueTarget);
        _loopContexts.Push(loopCtx);
        
        LowerExpr(stmt.Condition, instructions);
        
        var exitPatch = new BackpatchList();
        instructions.Add((ushort)OpCode.JMP_FALSE);
        exitPatch.Add(instructions.Count);
        instructions.Add(0);
        
        LowerStatement(stmt.Body, instructions);
        
        loopCtx.ContinueJumps.PatchAll(instructions);
        
        instructions.Add((ushort)OpCode.JMP);
        instructions.Add(continueTarget);
        
        exitPatch.PatchAll(instructions);
        loopCtx.BreakJumps.PatchAll(instructions);
        _loopContexts.Pop();
    }

    private void LowerExpr(BoundExpr expr, List<ushort> instructions)
    {
        switch (expr)
        {
            case BoundIntegerLiteralExpr i:
                LowerIntLiteral(i, instructions);
                break;
            case BoundFloatLiteralExpr f:
                LowerFloatLiteral(f, instructions);
                break;
            case BoundStringLiteralExpr s:
                LowerStringLiteral(s, instructions);
                break;
            case BoundBoolLiteralExpr b:
                instructions.Add((ushort)OpCode.PUSH_BOOL);
                instructions.Add(b.Value ? (ushort)1 : (ushort)0);
                break;
            case BoundNullLiteralExpr:
                instructions.Add((ushort)OpCode.PUSH_NULL);
                break;
            case BoundVariableExpr v:
                LowerVariable(v, instructions);
                break;
            case BoundAssignExpr a:
                LowerAssign(a, instructions);
                break;
            case BoundBinaryExpr b:
                LowerBinary(b, instructions);
                break;
            case BoundUnaryExpr u:
                LowerUnary(u, instructions);
                break;
            case BoundGroupingExpr g:
                LowerExpr(g.Inner, instructions);
                break;
            case BoundCallExpr c:
                LowerCall(c, instructions);
                break;
            case BoundGetExpr g:
                LowerGet(g, instructions);
                break;
            case BoundNewExpr n:
                LowerNew(n, instructions);
                break;
            case BoundErrorExpr:
                break; // already reported
            default:
                logger.Warning(Phase, $"Unknown expression type: {expr.GetType().Name}");
                break;
        }
    }

    private void LowerIntLiteral(BoundIntegerLiteralExpr expr, List<ushort> instructions)
    {
        var idx = _constants.AddInt(expr.Value);
        instructions.Add((ushort)OpCode.PUSH_INT);
        instructions.Add(idx);
    }
    
    private void LowerFloatLiteral(BoundFloatLiteralExpr expr, List<ushort> instructions)
    {
        var idx = _constants.AddFloat(expr.Value);
        instructions.Add((ushort)OpCode.PUSH_FLOAT);
        instructions.Add(idx);
    }

    private void LowerStringLiteral(BoundStringLiteralExpr expr, List<ushort> instructions)
    {
        var idx = _constants.AddString(expr.Value);
        instructions.Add((ushort)OpCode.PUSH_STRING);
        instructions.Add(idx);
    }

    private void LowerVariable(BoundVariableExpr expr, List<ushort> instructions)
    {
        if (_localSlots.TryGetValue(expr.Name, out var slot))
        {
            instructions.Add((ushort)OpCode.LOAD_LOCAL);
            instructions.Add(slot);
            return;
        }
        
        // Could be a type reference (e.g., People.Student) — handled by LowerGet
        // If we get here, it's an error
        logger.Error(Phase, $"Unresolved variable '{expr.Name}' during lowering.");
    }

    private void LowerAssign(BoundAssignExpr expr, List<ushort> instructions)
    {
        LowerExpr(expr.Value, instructions);

        switch (expr.Target)
        {
            case BoundVariableExpr v:
                if (_localSlots.TryGetValue(v.Name, out var slot))
                {
                    instructions.Add((ushort)OpCode.STORE_LOCAL);
                    instructions.Add(slot);
                }
                else
                    logger.Error(Phase, $"Unresolved assignment target '{v.Name}'.");
                break;
            case BoundGetExpr g:
                
                //Check if it is a property - emit set call
                if (TryResolvePropertySetter(g.Object.Type, g.MemberName, out var setterIdx))
                {
                    LowerExpr(g.Object, instructions);
                    LowerExpr(expr.Value, instructions);
                    instructions.Add((ushort)OpCode.CALL);
                    instructions.Add(setterIdx);
                    instructions.Add(1); // setters always have one arg
                    return;
                }
                
                LowerExpr(g, instructions);
                // Otherwise, it's a field access
                var fieldIndex = ResolveFieldIndex(g.Object.Type, g.MemberName);
                instructions.Add((ushort)OpCode.SET_FIELD);
                instructions.Add(fieldIndex);
                break;
            default:
                logger.Error(Phase, $"Unknown assignment target '{expr.Target.GetType().Name}'.");
                break;
        }
    }
    
    private void LowerBinary(BoundBinaryExpr expr, List<ushort> instructions)
    {
        LowerExpr(expr.Left, instructions);
        LowerExpr(expr.Right, instructions);

        // String concatenation via + operator
        if (expr.Operator.Type == TokenType.Plus &&
            (expr.Left.Type is BoundPrimitiveType { Name: "string" } ||
             expr.Right.Type is BoundPrimitiveType { Name: "string" }))
        {
            instructions.Add((ushort)OpCode.CONCAT);
            return;
        }

        var opCode = expr.Operator.Type switch
        {
            TokenType.Plus => OpCode.ADD,
            TokenType.Minus => OpCode.SUB,
            TokenType.Star => OpCode.MUL,
            TokenType.Slash => OpCode.DIV,
            TokenType.Percent => OpCode.MOD,
            TokenType.EqualEqual => OpCode.EQ,
            TokenType.BangEqual => OpCode.NEQ,
            TokenType.Less => OpCode.LT,
            TokenType.LessEqual => OpCode.LTE,
            TokenType.Greater => OpCode.GT,
            TokenType.GreaterEqual => OpCode.GTE,
            TokenType.AmpAmp => OpCode.AND,
            TokenType.PipePipe => OpCode.OR,
            _ => throw new InvalidOperationException($"Unknown binary operator: {expr.Operator.Lexeme}")
        };

        instructions.Add((ushort)opCode);
    }
    
    private void LowerUnary(BoundUnaryExpr expr, List<ushort> instructions)
    {
        LowerExpr(expr.Operand, instructions);

        var opCode = expr.Operator.Type switch
        {
            TokenType.Minus => OpCode.NEG,
            TokenType.Bang => OpCode.NOT,
            _ => throw new InvalidOperationException($"Unknown unary operator: {expr.Operator.Lexeme}")
        };

        instructions.Add((ushort)opCode);
    }

    private void LowerCall(BoundCallExpr expr, List<ushort> instructions)
    {
        foreach (var arg in expr.Arguments)
            LowerExpr(arg, instructions);
        
        var argCount = (ushort)expr.Arguments.Count;

        switch (expr.ResolvedTarget)
        {
            case BoundBuiltInFunction fn:
                if (StdLib.TryResolveFunction(fn.Name, out var fnIndex))
                {
                    instructions.Add((ushort)OpCode.CALL_EXTERN);
                    instructions.Add(StdLib.ModuleIndex);
                    instructions.Add(fnIndex);
                    instructions.Add(argCount);
                }
                else
                    logger.Error(Phase, $"Unresolved built-in function '{fn.Name}'.");

                break;
            case BoundBuiltInMethod m:
                if (expr.Callee is BoundGetExpr g)
                    LowerExpr(g, instructions);
                if (StdLib.TryResolveObjectMethod(m.Name, out var mIdx))
                {
                    instructions.Add((ushort)OpCode.CALL_EXTERN);
                    instructions.Add(StdLib.ModuleIndex);
                    instructions.Add(mIdx);
                    instructions.Add(argCount);
                }
                else
                {
                    logger.Error(Phase, $"Unresolved object method '{m.Name}'.");
                }

                break;
            case BoundMethod m:
                var key = GetCallableKey(m);
                if (_methodIndices.TryGetValue(key, out var index))
                {
                    instructions.Add((ushort)OpCode.CALL);
                    instructions.Add(index);
                    instructions.Add(argCount);
                }
                else
                {
                    logger.Error(Phase, $"Unresolved method '{m.Name}'.");
                }

                break;
            case null:
                logger.Error(Phase, "Call expression has no resolved target.");
                break;
            default:
                logger.Error(Phase, $"Unknown callable method target type '{expr.ResolvedTarget.GetType().Name}'.");
                break;
        }
        /*
         * TODO: This might not be complete yet. I have not seen anything that suggests that we have any calling into getters and setters
         */
    }

    private void LowerGet(BoundGetExpr expr, List<ushort> instructions)
    {
        LowerExpr(expr.Object, instructions);
        
        // Check for a property getter
        if (TryResolvePropertyGetter(expr.Object.Type, expr.MemberName, out var getterIdx))
        {
            instructions.Add((ushort)OpCode.CALL);
            instructions.Add(getterIdx);
            instructions.Add(0); // getters never have args
            return;
        }
        
        // Otherwise, it's a field access
        var fieldIndex = ResolveFieldIndex(expr.Object.Type, expr.MemberName);
        instructions.Add((ushort)OpCode.GET_FIELD);
        instructions.Add(fieldIndex);
    }

    private void LowerNew(BoundNewExpr expr, List<ushort> instructions)
    {
        if (!_typeIndices.TryGetValue(expr.TargetType.DisplayName, out var typeIndex))
        {
            logger.Error(Phase, $"Unresolved type '{expr.TargetType.DisplayName}' during lowering");
            return;
        }
        
        instructions.Add((ushort)OpCode.ALLOC);
        instructions.Add(typeIndex);
        
        // Initialize fields that have initializers
        LowerFieldInitializers(expr.TargetType, instructions);
        
        foreach (var arg in expr.Arguments)
            LowerExpr(arg, instructions);
        
        if (expr.ResolvedConstructor is not null)
        {
            var ctorKey = GetCallableKey(expr.ResolvedConstructor);
            if (_methodIndices.TryGetValue(ctorKey, out var ctorIdx))
            {
                instructions.Add((ushort)OpCode.CALL);
                instructions.Add(ctorIdx);
                instructions.Add((ushort)expr.Arguments.Count);
            }
            else
                logger.Error(Phase, $"Unresolved constructor '{ctorKey}' during lowering.");
        }
    }

    private void LowerFieldInitializers(BoundType targetType, List<ushort> instructions)
    {
        if (targetType is not BoundUserDefinedType userType) return; // This should not happen, but if it does, we have nothing to initialize
        if (userType.Declaration is not ClassDecl) return; // At the moment, only classes can have fields

        if (!_boundClasses.TryGetValue(userType.QualifiedName, out var cls)) return;

        foreach (var field in cls.Fields)
        {
            if (field.Initializer is null) continue;
            
            instructions.Add((ushort)OpCode.DUP);
            LowerExpr(field.Initializer, instructions);
            var fieldIdx = ResolveFieldIndex(targetType, field.Name);
            instructions.Add((ushort)OpCode.SET_FIELD);
            instructions.Add(fieldIdx);
        }
    }

    #endregion

    #region Utilities

    private bool TryResolvePropertyGetter(BoundType objType, string memberName, out ushort methodIdx)
    {
        methodIdx = 0;
        if (objType is not BoundUserDefinedType userType) return false;
        if (!_boundClasses.TryGetValue(userType.QualifiedName, out var cls)) return false;

        var prop = cls.Properties.Find(p => p.Name == memberName);
        if (prop?.Getter is null) return false;

        var key = GetCallableKey(prop.Getter);
        return _methodIndices.TryGetValue(key, out methodIdx);
    }

    private bool TryResolvePropertySetter(BoundType objType, string memberName, out ushort methodIdx)
    {
        methodIdx = 0;
        if (objType is not BoundUserDefinedType userType) return false;
        if (!_boundClasses.TryGetValue(userType.QualifiedName, out var cls)) return false;
        
        var prop = cls.Properties.Find(p => p.Name == memberName);
        if (prop?.Setter is null) return false;
        
        var key = GetCallableKey(prop.Setter);
        return _methodIndices.TryGetValue(key, out methodIdx);
    }

    private ushort ResolveFieldIndex(BoundType objType, string memberName)
    {
        if (objType is BoundUserDefinedType userType && _typeIndices.TryGetValue(userType.QualifiedName, out var typeIdx))
        {
            var type = _types.Get(typeIdx);
            var fieldIdx = type.Fields.FindIndex(f => f.Name == memberName);
            if (fieldIdx >= 0) return (ushort)fieldIdx;
        }
        
        logger.Error(Phase, $"Unresolved field '{memberName}' on '{objType.DisplayName}' during lowering");
        return 0;
    }

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

    // private ushort GetOrAddStdLibImport()
    // {
    //     const string stdlibName = "stdlib";
    //     var existing = _imports.All.FirstOrDefault(i => i.ModuleName == stdlibName);
    //     if (existing is not null)
    //         return (ushort)_imports.All.ToList().IndexOf(existing);
    //     return _imports.Add(stdlibName, StdLib.ModuleIndex);
    // }

    private ushort AllocateSlot()
    {
        var slot = _localCount;
        _localCount++;
        return slot;
    }
    
    private void ResetForModule()
    {
        _constants = new ConstantPool();
        _types = new TypeTable();
        _methods = new MethodTable();
        _imports = new ImportTable();
        _methodIndices.Clear();
        _typeIndices.Clear();
        _boundClasses.Clear();
        ResetLocals();
    }

    private void ResetLocals()
    {
        _localCount = 0;
        _localSlots.Clear();
        _loopContexts.Clear();
    }

    #endregion
}