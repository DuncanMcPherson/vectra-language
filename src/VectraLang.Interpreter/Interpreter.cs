using VectraLang.Ast.AstNodes;
using VectraLang.Ast.Tokens;
using VectraLang.ModuleLoader;

namespace VectraLang.Interpreter;

public sealed class Interpreter
{
    private readonly VectraEnvironment _globals = new();
    private VectraEnvironment _environment;

    public Interpreter()
    {
        _environment = _globals;
        RegisterBuiltins();
    }

    private void RegisterBuiltins()
    {
        _globals.Define("PrintLine", new NativeFunction(1, args =>
        {
            Console.WriteLine(args[0].RawValue?.ToString() ?? "null");
            return NullValue.Instance;
        }));

        _globals.Define("Print", new NativeFunction(1, args =>
        {
            Console.Write(args[0].RawValue?.ToString() ?? "null");
            return NullValue.Instance;
        }));

        _globals.Define("ReadLine", new NativeFunction(0, _ =>
        {
            var line = Console.ReadLine();
            return new StringValue(line!);
        }));
    }

    public void Interpret(MergedPackage package)
    {
        foreach (var mod in package.Modules)
            foreach (var space in mod.SpaceDecls)
                RegisterTypes(space);

        var executables = package.Modules.Where(m => m.IsExecutable).ToList();
        if (executables.Count == 0)
            throw new RuntimeException("No executable module found.");

        VectraMethod? main = null;
        foreach (var module in executables)
        {
            foreach (var space in module.SpaceDecls)
                if (TryFindInSpace(space, out main) && main is not null)
                    break;
        }
        
        if (main is null)
            throw new RuntimeException("No entry point found. Expected 'Main' function.");

        main.Call(this, []);
    }

    public void Interpret(MergedModule module)
    {
        foreach (var space in module.SpaceDecls)
            RegisterTypes(space);

        VectraMethod? main = null;
        
        foreach (var space in module.SpaceDecls)
            if (TryFindInSpace(space, out main) && main is not null)
                break;
        
        if (main is null)
            throw new RuntimeException("No entry point found. Expected 'Main' function.");
        
        main.Call(this, new List<RuntimeValue>());
    }

    public void Interpret(VectraFile file)
    {
        RegisterTypes(file.Space);

        if (!TryFindEntryPoint(file, out VectraMethod? main))
            throw new RuntimeException("No entry point found. Expected 'main' function.");
        main!.Call(this, new List<RuntimeValue>());
    }

    private void RegisterTypes(SpaceDecl space)
    {
        foreach (var child in space.Children)
        {
            RegisterTypes(child);
        }

        foreach (var decl in space.Declarations)
        {
            switch (decl)
            {
                case ClassDecl cls:
                    _globals.Define(cls.Name.Lexeme, cls);
                    break;
                case EnumDecl e:
                    _globals.Define(e.Name.Lexeme, InstantiateEnum(e));
                    break;
                case InterfaceDecl:
                    break;
            }
        }
    }

    private VectraEnum InstantiateEnum(EnumDecl e)
    {
        var variants = new Dictionary<string, VectraEnumVariant>();
        foreach (var variant in e.Variants)
        {
            var fields = new VectraEnvironment();

            for (var i = 0; i < e.Parameters.Count; i++)
            {
                var argValue = i < variant.Arguments.Count
                    ? Evaluate(variant.Arguments[i])
                    : DefaultValue(e.Parameters[i].Type);
                fields.Define(e.Parameters[i].Name.Lexeme, argValue);
            }

            variants[variant.Name.Lexeme] = new VectraEnumVariant(e, variant, fields);
        }

        return new VectraEnum(e, variants);
    }

    private bool TryFindEntryPoint(VectraFile file, out VectraMethod? main)
    {
        return TryFindInSpace(file.Space, out main);
    }

    private bool TryFindInSpace(SpaceDecl space, out VectraMethod? main)
    {
        foreach (var decl in space.Declarations)
        {
            if (decl is not ClassDecl cls) continue;
            var method = cls.Methods.FirstOrDefault(m =>
                m.Name.Lexeme == "Main" &&
                m is { ReturnType: PrimitiveTypeNode { TypeToken.Lexeme: "void" }, Parameters.Count: 0 });
            if (method is not null)
            {
                var instance = InstantiateClass(cls, new List<RuntimeValue>());
                var env = _environment.CreateChild();
                env.Define("this", instance);
                main = new VectraMethod(method, env);
                return true;
            }
        }

        foreach (var child in space.Children)
        {
            if (TryFindInSpace(child, out main))
                return true;
        }

        main = null;
        return false;
    }

    internal RuntimeValue ExecuteBlock(BlockStmt block, VectraEnvironment environment)
    {
        var previous = _environment;
        try
        {
            _environment = environment;
            foreach (var stmt in block.Statements)
                Execute(stmt);
        }
        catch (ReturnException ret)
        {
            return ret.Value;
        }
        finally
        {
            _environment = previous;
        }

        return NullValue.Instance;
    }

    private void Execute(Stmt stmt)
    {
        switch (stmt)
        {
            case VarDeclStmt v: ExecuteVarDecl(v); break;
            case ExprStmt e: Evaluate(e.Expression); break;
            case IfStmt i: ExecuteIf(i); break;
            case WhileStmt w: ExecuteWhile(w); break;
            case ForStmt f: ExecuteFor(f); break;
            case ReturnStmt r: ExecuteReturn(r); break;
            case BlockStmt b: ExecuteBlock(b, _environment.CreateChild()); break;
            case BreakStmt: throw new BreakException();
            case ContinueStmt: throw new ContinueException();
            default:
                throw new RuntimeException($"Unknown statement type: {stmt.GetType().Name}");
        }
    }

    private void ExecuteVarDecl(VarDeclStmt stmt)
    {
        RuntimeValue value = stmt.Initializer is not null
            ? Evaluate(stmt.Initializer)
            : DefaultValue(stmt.Type);
        _environment.Define(stmt.Name.Lexeme, value);
    }

    private void ExecuteIf(IfStmt stmt)
    {
        var condition = Evaluate(stmt.Condition);
        if (IsTruthy(condition))
            Execute(stmt.ThenBranch);
        else if (stmt.ElseBranch is not null)
            Execute(stmt.ElseBranch);
    }

    private void ExecuteWhile(WhileStmt stmt)
    {
        while (IsTruthy(Evaluate(stmt.Condition)))
        {
            try
            {
                Execute(stmt.Body);
            }
            catch (BreakException)
            {
                break;
            }
            catch (ContinueException)
            {
                // ReSharper disable once RedundantJumpStatement
                continue;
            }
        }
    }

    private void ExecuteFor(ForStmt stmt)
    {
        var loopEnv = _environment.CreateChild();
        var previous = _environment;
        _environment = loopEnv;

        try
        {
            if (stmt.Initializer is not null)
                Execute(stmt.Initializer);
            while (stmt.Condition is null || IsTruthy(Evaluate(stmt.Condition)))
            {
                try
                {
                    Execute(stmt.Body);
                }
                catch (BreakException)
                {
                    break;
                }
                catch (ContinueException)
                {
                    if (stmt.Increment is not null)
                        Evaluate(stmt.Increment);
                    continue;
                }

                if (stmt.Increment is not null)
                    Evaluate(stmt.Increment);
            }
        }
        finally
        {
            _environment = previous;
        }
    }

    private void ExecuteReturn(ReturnStmt stmt)
    {
        RuntimeValue value = stmt.Value is not null
            ? Evaluate(stmt.Value)
            : NullValue.Instance;
        throw new ReturnException(value);
    }

    internal RuntimeValue DefaultValue(TypeNode type)
    {
        if (type is PrimitiveTypeNode p)
        {
            return p.TypeToken.Lexeme switch
            {
                "int" => new IntValue(0),
                "float" => new FloatValue(0f),
                "string" => new StringValue(""),
                "bool" => new BoolValue(false),
                _ => NullValue.Instance
            };
        }

        return NullValue.Instance;
    }

    private bool IsTruthy(RuntimeValue value) => value switch
    {
        BoolValue b => b.Value,
        NullValue => false,
        IntValue i => i.Value != 0,
        FloatValue f => f.Value != 0f,
        StringValue s => !string.IsNullOrEmpty(s.Value),
        _ => true
    };

    private VectraObject InstantiateClass(ClassDecl cls, List<RuntimeValue> arguments)
    {
        var fields = new VectraEnvironment();
        var instance = new VectraObject(cls, fields);

        foreach (var field in cls.Fields)
        {
            var value = field.Initializer is not null
                ? Evaluate(field.Initializer)
                : DefaultValue(field.Type);
            fields.Define(field.Name.Lexeme, value);
        }

        var ctor = cls.Constructors.FirstOrDefault(c => c.Parameters.Count == arguments.Count);
        if (ctor is not null)
        {
            var env = _environment.CreateChild();
            env.Define("this", instance);
            for (var i = 0; i < ctor.Parameters.Count; i++)
                env.Define(ctor.Parameters[i].Name.Lexeme, arguments[i]);
            ExecuteBlock(ctor.Body, env);
        }
        else if (arguments.Count > 0)
        {
            throw new RuntimeException(
                $"No constructor with {arguments.Count} arguments found for class '{cls.Name.Lexeme}'");
        }

        return instance;
    }

    private RuntimeValue Evaluate(Expr expr)
    {
        return expr switch
        {
            IntegerLiteralExpr e => new IntValue(e.Value),
            FloatLiteralExpr e => new FloatValue(e.Value),
            StringLiteralExpr e => new StringValue(e.Value),
            BoolLiteralExpr e => new BoolValue(e.Value),
            NullLiteralExpr => NullValue.Instance,
            VariableExpr e => EvaluateVariable(e),
            AssignExpr e => EvaluateAssign(e),
            BinaryExpr e => EvaluateBinary(e),
            UnaryExpr e => EvaluateUnary(e),
            GroupingExpr e => Evaluate(e.Inner),
            CallExpr e => EvaluateCall(e),
            GetExpr e => EvaluateGet(e),
            OptionalGetExpr e => EvaluateOptionalGet(e),
            DestructureExpr e => EvaluateDestructure(e),
            NewExpr e => EvaluateNew(e),
            _ => throw new RuntimeException($"Unknown expression type: {expr.GetType().Name}")
        };
    }

    private RuntimeValue EvaluateNew(NewExpr expr)
    {
        var value = _environment.Get(expr.TypeName.Lexeme);
        if (value is not ClassDecl cls)
            throw new RuntimeException($"'{expr.TypeName.Lexeme}' is not a class.");
        var arguments = expr.Arguments.Select(Evaluate).ToList();
        return InstantiateClass(cls, arguments);
    }

    private RuntimeValue EvaluateVariable(VariableExpr expr)
    {
        var value = _environment.Get(expr.Name.Lexeme);
        return value as RuntimeValue ?? NullValue.Instance;
    }

    private RuntimeValue EvaluateAssign(AssignExpr expr)
    {
        var value = Evaluate(expr.Value);
        switch (expr.Target)
        {
            case VariableExpr v:
                _environment.Assign(v.Name.Lexeme, value);
                break;
            case GetExpr g:
                var obj = Evaluate(g.Object);
                if (obj is VectraObject vectraObject)
                {
                    var property = vectraObject.Declaration.Properties
                        .FirstOrDefault(p => p.Name.Lexeme == g.Name.Lexeme);
                    if (property?.Setter is not null)
                    {
                        var env = _environment.CreateChild();
                        env.Define("this", vectraObject);
                        env.Define("value", value);
                        ExecuteBlock(property.Setter, env);
                    }
                    else
                    {
                        vectraObject.SetField(g.Name.Lexeme, value);
                    }
                }
                else
                {
                    throw new RuntimeException(
                        $"Cannot assign to '{g.Name.Lexeme}' because it is not a class.");
                }

                break;
            case OptionalGetExpr og:
                var target = Evaluate(og.Object);
                if (target is NullValue) break;
                if (target is VectraObject vo)
                    vo.SetField(og.Name.Lexeme, value);
                break;
            default:
                throw new RuntimeException("Invalid assignment target.");
        }

        return value;
    }

    private RuntimeValue EvaluateGet(GetExpr expr)
    {
        var obj = Evaluate(expr.Object);
        if (obj is VectraObject instance)
        {
            if (instance.Fields.IsDefined(expr.Name.Lexeme))
                return instance.GetField(expr.Name.Lexeme);
            
            var method = instance.Declaration.Methods.FirstOrDefault(met => met.Name.Lexeme == expr.Name.Lexeme);
            if (method is not null)
            {
                var env = _environment.CreateChild();
                env.Define("this", instance);
                return new VectraMethod(method, env);
            }
            
            var property = instance.Declaration.Properties
                .FirstOrDefault(p => p.Name.Lexeme == expr.Name.Lexeme);
            if (property?.Getter is not null)
            {
                var env = _environment.CreateChild();
                env.Define("this", instance);
                return ExecuteBlock(property.Getter, env);
            }

            if (ObjectMethodsRegistry.Methods.TryGetValue(expr.Name.Lexeme, out var methodInfo))
                return methodInfo(obj);
            
            throw new RuntimeException($"Undefined member '{expr.Name.Lexeme}' on '{instance.TypeName}'.");
        }

        if (obj is VectraEnum ve)
        {
            if (ve.Variants.TryGetValue(expr.Name.Lexeme, out var variant))
                return variant;
            
            var method = ve.Declaration.Methods
                .FirstOrDefault(me => me.Name.Lexeme == expr.Name.Lexeme);
            if (method is not null)
            {
                var env = _environment.CreateChild();
                return new VectraMethod(method, env);
            }
            
            throw new RuntimeException($"Undefined member '{expr.Name.Lexeme}' on '{ve.TypeName}'.");
        }

        if (obj is VectraEnumVariant vev)
        {
            // FieldAccess
            if (vev.Fields.IsDefined(expr.Name.Lexeme))
                return vev.Fields.Get(expr.Name.Lexeme) as RuntimeValue ?? NullValue.Instance;
            
            var overrideMethod = vev.Variant.Overrides
                .FirstOrDefault(ov => ov.Name.Lexeme == expr.Name.Lexeme);
            if (overrideMethod is not null)
            {
                var env = _environment.CreateChild();
                env.Define("this", vev);
                return new VectraMethod(overrideMethod, env);
            }
            
            var method = vev.Enum.Methods
                .FirstOrDefault(me => me.Name.Lexeme == expr.Name.Lexeme);
            if (method is not null)
            {
                var env = _environment.CreateChild();
                env.Define("this", vev);
                return new VectraMethod(method, env);
            }
            
            if (ObjectMethodsRegistry.Methods.TryGetValue(expr.Name.Lexeme, out var nativeMethod))
                return nativeMethod(obj);

            throw new RuntimeException(
                $"Undefined member '{expr.Name.Lexeme}' on enum variant '{vev.TypeName}'.");
        }
        
        if (ObjectMethodsRegistry.Methods.TryGetValue(expr.Name.Lexeme, out var m))
            return m(obj);

        throw new RuntimeException($"Cannot access member '{expr.Name.Lexeme}' on value of type '{obj.TypeName}'.");
    }

    private RuntimeValue EvaluateOptionalGet(OptionalGetExpr expr)
    {
        var obj = Evaluate(expr.Object);
        if (obj is NullValue) return NullValue.Instance;

        return EvaluateGet(new GetExpr(expr.Object, expr.Name, expr.Location));
    }

    private RuntimeValue EvaluateCall(CallExpr expr)
    {
        var callee = Evaluate(expr.Callee);
        var arguments = expr.Arguments.Select(Evaluate).ToList();
        if (callee is CallableValue callable)
        {
            if (callable.Arity != arguments.Count)
                throw new RuntimeException($"Expected {callable.Arity} argument(s) but got {arguments.Count}.");

            return callable.Call(this, arguments);
        }

        throw new RuntimeException($"Cannot call '{expr.Callee}' because it is not callable.");
    }

    private RuntimeValue EvaluateBinary(BinaryExpr expr)
    {
        RuntimeValue left = Evaluate(expr.Left);
        RuntimeValue right = Evaluate(expr.Right);

        return expr.Operator.Type switch
        {
            // arithmetic
            TokenType.Plus => EvaluateAdd(left, right),
            TokenType.Minus => EvaluateArithmetic(left, right, (a, b) => a - b, (a, b) => a - b),
            TokenType.Star => EvaluateArithmetic(left, right, (a, b) => a * b, (a, b) => a * b),
            TokenType.Slash => EvaluateDivide(left, right, expr.Operator),
            TokenType.Percent => EvaluateArithmetic(left, right, (a, b) => a % b, (a, b) => a % b),

            // comparison
            TokenType.Less => EvaluateComparison(left, right, (a, b) => a < b, (a, b) => a < b),
            TokenType.LessEqual => EvaluateComparison(left, right, (a, b) => a <= b, (a, b) => a <= b),
            TokenType.Greater => EvaluateComparison(left, right, (a, b) => a > b, (a, b) => a > b),
            TokenType.GreaterEqual => EvaluateComparison(left, right, (a, b) => a >= b, (a, b) => a >= b),

            // equality
            TokenType.EqualEqual => new BoolValue(IsEqual(left, right)),
            TokenType.BangEqual => new BoolValue(!IsEqual(left, right)),

            // logical
            TokenType.AmpAmp => new BoolValue(IsTruthy(left) && IsTruthy(right)),
            TokenType.PipePipe => new BoolValue(IsTruthy(left) || IsTruthy(right)),

            _ => throw new RuntimeException($"Unknown binary operator '{expr.Operator.Lexeme}'.")
        };
    }

    private RuntimeValue EvaluateAdd(RuntimeValue left, RuntimeValue right)
    {
        if (left is IntValue li && right is IntValue ri)
            return new IntValue(li.Value + ri.Value);
        if (left is FloatValue lf && right is FloatValue rf)
            return new FloatValue(lf.Value + rf.Value);
        if (left is StringValue ls && right is StringValue rs)
            return new StringValue(ls.Value + rs.Value);
        if (left is StringValue l)
        {
            var rightString = right.RawValue!.ToString();
            return new StringValue(l.Value + rightString);
        }

        if (right is StringValue r)
        {
            var leftString = left.RawValue!.ToString();
            return new StringValue(leftString + r.Value);
        }

        // int + float promotion
        if (left is IntValue lint && right is FloatValue rfloat)
            return new FloatValue(lint.Value + rfloat.Value);
        if (left is FloatValue lfloat && right is IntValue rint)
            return new FloatValue(lfloat.Value + rint.Value);

        throw new RuntimeException(
            $"Cannot apply '+' to types '{left.TypeName}' and '{right.TypeName}'.");
    }

    private RuntimeValue EvaluateArithmetic(
        RuntimeValue left, RuntimeValue right,
        Func<int, int, int> intOp,
        Func<float, float, float> floatOp)
    {
        if (left is IntValue li && right is IntValue ri)
            return new IntValue(intOp(li.Value, ri.Value));
        if (left is FloatValue lf && right is FloatValue rf)
            return new FloatValue(floatOp(lf.Value, rf.Value));
        if (left is IntValue lint && right is FloatValue rfloat)
            return new FloatValue(floatOp(lint.Value, rfloat.Value));
        if (left is FloatValue lfloat && right is IntValue rint)
            return new FloatValue(floatOp(lfloat.Value, rint.Value));

        throw new RuntimeException(
            $"Cannot apply arithmetic to types '{left.TypeName}' and '{right.TypeName}'.");
    }

    private RuntimeValue EvaluateDivide(RuntimeValue left, RuntimeValue right, Token op)
    {
        if (right is IntValue { Value: 0 })
            throw new RuntimeException("Division by zero.", op.Location);
        if (right is FloatValue { Value: 0f })
            throw new RuntimeException("Division by zero.", op.Location);

        return EvaluateArithmetic(left, right, (a, b) => a / b, (a, b) => a / b);
    }

    private RuntimeValue EvaluateComparison(
        RuntimeValue left, RuntimeValue right,
        Func<int, int, bool> intOp,
        Func<float, float, bool> floatOp)
    {
        if (left is IntValue li && right is IntValue ri)
            return new BoolValue(intOp(li.Value, ri.Value));
        if (left is FloatValue lf && right is FloatValue rf)
            return new BoolValue(floatOp(lf.Value, rf.Value));
        if (left is IntValue lint && right is FloatValue rfloat)
            return new BoolValue(floatOp(lint.Value, rfloat.Value));
        if (left is FloatValue lfloat && right is IntValue rint)
            return new BoolValue(floatOp(lfloat.Value, rint.Value));

        throw new RuntimeException(
            $"Cannot compare types '{left.TypeName}' and '{right.TypeName}'.");
    }

    private bool IsEqual(RuntimeValue left, RuntimeValue right)
    {
        if (left is NullValue && right is NullValue) return true;
        if (left is NullValue || right is NullValue) return false;
        return left.RawValue?.Equals(right.RawValue) ?? false;
    }

    private RuntimeValue EvaluateUnary(UnaryExpr expr)
    {
        RuntimeValue right = Evaluate(expr.Right);

        return expr.Operator.Type switch
        {
            TokenType.Bang => new BoolValue(!IsTruthy(right)),
            TokenType.Minus => right switch
            {
                IntValue i => new IntValue(-i.Value),
                FloatValue f => new FloatValue(-f.Value),
                _ => throw new RuntimeException(
                    $"Cannot negate type '{right.TypeName}'.")
            },
            _ => throw new RuntimeException(
                $"Unknown unary operator '{expr.Operator.Lexeme}'.")
        };
    }

    private RuntimeValue EvaluateDestructure(DestructureExpr expr)
    {
        RuntimeValue value = Evaluate(expr.Value);

        if (value is not VectraObject obj)
            throw new RuntimeException("Can only destructure object types.");

        foreach (var name in expr.Names)
        {
            if (!obj.Fields.IsDefined(name.Lexeme))
                throw new RuntimeException(
                    $"Property '{name.Lexeme}' does not exist on type '{obj.TypeName}'.");

            var fieldValue = obj.Fields.Get(name.Lexeme) as RuntimeValue ?? NullValue.Instance;
            _environment.Define(name.Lexeme, fieldValue);
        }

        return NullValue.Instance;
    }
}