using VectraLang.Analysis.Context;
using VectraLang.Ast.Tokens;
using VectraLang.Binding;
using VectraLang.Binding.Nodes;
using VectraLang.Binding.Scope;
using VectraLang.Core;
using VectraLang.Core.Diagnostics;

namespace VectraLang.Analysis;

public class Analyzer(IVectraLogger logger)
{
    private const string Phase = "Analysis";
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private BindingScope _scope;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public void Analyze(BindingResult result)
    {
        _scope = result.Scope;
        switch (result.BoundRoot)
        {
            case BoundFile f:
                AnalyzeFile(f);
                break;
            case BoundModule m:
                AnalyzeModule(m);
                break;
            case BoundPackage p:
                AnalyzePackage(p);
                break;
            default:
                logger.Warning(Phase, $"Unexpected root node type: {result.BoundRoot!.GetType()}");
                break;
        }
    }

    private void AnalyzePackage(BoundPackage package)
    {
        logger.Debug(Phase, $"Analyzing package '{package.PackageName}'");
        foreach (var module in package.Modules)
            AnalyzeModule(module);
    }

    private void AnalyzeModule(BoundModule module)
    {
        logger.Debug(Phase, $"Analyzing module '{module.Name}'");
        foreach (var space in module.Spaces)
            AnalyzeSpace(space);
    }

    private void AnalyzeFile(BoundFile file)
    {
        logger.Debug(Phase, "Analyzing file");
        AnalyzeSpace(file.Space);
    }

    private void AnalyzeSpace(BoundSpace space)
    {
        foreach (var decl in space.Declarations)
            AnalyzeTypeDecl(decl);
        foreach (var child in space.Children)
            AnalyzeSpace(child);
    }

    private void AnalyzeTypeDecl(BoundTypeDecl decl)
    {
        switch (decl)
        {
            case BoundClass c:
                AnalyzeClass(c);
                break;
            case BoundInterface i:
                AnalyzeInterface(i);
                break;
            case BoundEnum e:
                AnalyzeEnum(e);
                break;
            default:
                logger.Error(Phase, $"Unknown type declaration '{decl.QualifiedName}'");
                break;
        }
    }

    private void AnalyzeClass(BoundClass c)
    {
        logger.Debug(Phase, $"Analyzing class '{c.QualifiedName}'");
        foreach (var field in c.Fields)
            AnalyzeField(field);
        foreach (var method in c.Methods)
            AnalyzeCallable(method, new AnalysisContext { ExpectedReturnType = method.ReturnType });
        foreach (var ctor in c.Constructors)
            AnalyzeCallable(ctor, new AnalysisContext { ExpectedReturnType = new BoundPrimitiveType("void") });
        foreach (var prop in c.Properties)
            AnalyzeProperty(prop);
    }

    private void AnalyzeInterface(BoundInterface i)
    {
        logger.Debug(Phase, $"Analyzing interface '{i.QualifiedName}'");
        // nothing to truly analyze
    }

    private void AnalyzeEnum(BoundEnum e)
    {
        logger.Debug(Phase, $"Analyzing enum '{e.QualifiedName}'");
        
        foreach (var field in e.Fields)
            AnalyzeField(field);
        
        foreach (var method in e.Methods)
            AnalyzeCallable(method, new AnalysisContext { ExpectedReturnType = method.ReturnType });

        foreach (var variant in e.Variants)
            AnalyzeEnumVariant(variant, e);
    }

    private void AnalyzeEnumVariant(BoundEnumVariant variant, BoundEnum parent)
    {
        logger.Debug(Phase, $"Analyzing enum variant '{variant.Name}'");

        if (variant.Arguments.Count != parent.Fields.Count)
        {
            logger.Error(Phase, $"Enum variant '{variant.Name}' has {variant.Arguments.Count} arguments " +
                                $"but enum '{parent.QualifiedName}' has {parent.Fields.Count} fields", variant.Source.Location);
            return;
        }

        for (var i = 0; i < variant.Arguments.Count; i++)
        {
            var argType = AnalyzeExpr(variant.Arguments[i], new AnalysisContext());
            var fieldType = parent.Fields[i].Type;
            if (!IsAssignableFrom(fieldType, argType)) 
                logger.Error(Phase, $"Enum variant '{variant.Name}' argument {i + 1} is '{argType}' " +
                                    $"but expected '{fieldType}'", variant.Source.Location);
        }

        foreach (var method in variant.Overrides)
            AnalyzeCallable(method, new AnalysisContext { ExpectedReturnType = method.ReturnType });
    }

    private void AnalyzeField(BoundField field)
    {
        logger.Debug(Phase, $"Analyzing field '{field.Name}'");
        if (field.Initializer is null) return;
        
        var initType = AnalyzeExpr(field.Initializer, new AnalysisContext());
        if (!IsAssignableFrom(field.Type, initType))
            logger.Error(Phase, $"Cannot assign '{initType}' to '{field.Type}'", GetLocation(field.Initializer));
    }

    private void AnalyzeProperty(BoundProperty prop)
    {
        logger.Debug(Phase, $"Analyzing property '{prop.Name}'");

        if (prop.Getter is not null)
            AnalyzeCallable(prop.Getter, new AnalysisContext { ExpectedReturnType = prop.Type });
        if (prop.Setter is not null)
            AnalyzeCallable(prop.Setter, new AnalysisContext { ExpectedReturnType = new BoundPrimitiveType("void") });
    }

    private void AnalyzeCallable(BoundCallable callable, AnalysisContext ctx)
    {
        logger.Debug(Phase, $"Analyzing callable '{callable.Name}'");

        if (!_scope.TryGetResolvedBody(callable, out var body) || body is null)
        {
            logger.Warning(Phase, $"No resolved body found for '{callable.Name}'");
            return;
        }

        var statements = body switch
        {
            BoundMethodBody m => m.Statements,
            BoundConstructorBody c => c.Statements,
            BoundPropertyGetterBody g => g.Statements,
            BoundPropertySetterBody s => s.Statements,
            _ => null,
        };

        if (statements is null)
        {
            logger.Warning(Phase, $"Unknown body type for '{callable.Name}'");
            return;
        }

        foreach (var stmt in statements)
            AnalyzeStatement(stmt, ctx);

        var returnType = ctx.ExpectedReturnType;
        if (returnType is not BoundPrimitiveType { Name: "void" } &&
            callable is BoundMethod or BoundPropertyGetter &&
            !ctx.HasReturn)
        {
            logger.Error(Phase, $"Callable '{callable.Name}' does not return a value");
        }
    }

    private void AnalyzeStatement(BoundStmt stmt, AnalysisContext context)
    {
        switch (stmt)
        {
            case BoundBlockStmt b:
                AnalyzeBlock(b, context);
                break;
            case BoundVarDeclStmt varDecl:
                AnalyzeVarDecl(varDecl, context);
                break;
            case BoundExprStmt exprStmt:
                AnalyzeExpr(exprStmt.Expression, context);
                break;
            case BoundIfStmt ifStmt:
                AnalyzeIf(ifStmt, context);
                break;
            case BoundWhileStmt whileStmt:
                AnalyzeWhile(whileStmt, context);
                break;
            case BoundForStmt forStmt:
                AnalyzeFor(forStmt, context);
                break;
            case BoundReturnStmt returnStmt:
                AnalyzeReturn(returnStmt, context);
                break;
            case BoundBreakStmt:
                if (!context.IsInsideLoop)
                    logger.Error(Phase, "'break' used outside of a loop.",
                        GetLocation(stmt));
                break;
            case BoundContinueStmt:
                if (!context.IsInsideLoop)
                    logger.Error(Phase, "'continue' used outside of a loop.",
                        GetLocation(stmt));
                break;
            case BoundErrorStmt:
                break; // already reported during parsing
        }
    }

    private void AnalyzeBlock(BoundBlockStmt block, AnalysisContext context)
    {
        var hasReturned = false;
        foreach (var stmt in block.Statements)
        {
            if (hasReturned)
            {
                logger.Warning(Phase, "Unreachable code detected.", GetLocation(stmt));
                break;
            }

            AnalyzeStatement(stmt, context);
            if (stmt is BoundReturnStmt)
                hasReturned = true;
        }
    }

    private void AnalyzeVarDecl(BoundVarDeclStmt decl, AnalysisContext context)
    {
        if (decl.Initializer is null) return;

        var initType = AnalyzeExpr(decl.Initializer, context);
        if (!IsAssignableFrom(decl.Type, initType))
            logger.Error(Phase, $"Cannot assign '{initType}' to '{decl.Type}'", GetLocation(decl.Initializer));
    }

    private void AnalyzeIf(BoundIfStmt stmt, AnalysisContext context)
    {
        var condType = AnalyzeExpr(stmt.Condition, context);
        if (condType is not BoundPrimitiveType { Name: "bool" })
            logger.Error(Phase, "If condition must be a boolean expression", GetLocation(stmt.Condition));
        var thenContext = context.Clone();
        AnalyzeStatement(stmt.ThenBranch, thenContext);

        if (stmt.ElseBranch is not null)
        {
            var elseContext = context.Clone();
            AnalyzeStatement(stmt.ElseBranch, elseContext);
            context.HasReturn = thenContext.HasReturn && elseContext.HasReturn;
        }
    }

    private void AnalyzeWhile(BoundWhileStmt stmt, AnalysisContext context)
    {
        var condType = AnalyzeExpr(stmt.Condition, context);
        if (condType is not BoundPrimitiveType { Name: "bool" })
            logger.Error(Phase, "While condition must be a boolean expression", GetLocation(stmt.Condition));
        context.EnterLoop();
        AnalyzeStatement(stmt.Body, context);
        context.ExitLoop();
    }

    private void AnalyzeFor(BoundForStmt stmt, AnalysisContext context)
    {
        if (stmt.Initializer is not null)
            AnalyzeStatement(stmt.Initializer, context);

        if (stmt.Condition is not null)
        {
            var condType = AnalyzeExpr(stmt.Condition, context);
            if (condType is not BoundPrimitiveType { Name: "bool" })
                logger.Error(Phase, "For condition must be a boolean expression", GetLocation(stmt.Condition));
        }

        if (stmt.Increment is not null)
            AnalyzeExpr(stmt.Increment, context);

        context.EnterLoop();
        AnalyzeStatement(stmt.Body, context);
        context.ExitLoop();
    }

    private void AnalyzeReturn(BoundReturnStmt stmt, AnalysisContext context)
    {
        context.HasReturn = true;

        if (stmt.Value is null)
        {
            if (context.ExpectedReturnType is not BoundPrimitiveType { Name: "void" })
                logger.Error(Phase, "Non-void method must return a value", GetLocation(stmt));
            return;
        }

        var returnType = AnalyzeExpr(stmt.Value, context);
        if (!IsAssignableFrom(context.ExpectedReturnType!, returnType))
            logger.Error(Phase, $"Cannot return '{returnType}' from method expecting '{context.ExpectedReturnType}'.",
                GetLocation(stmt.Value));
    }

    private BoundType AnalyzeExpr(BoundExpr expr, AnalysisContext context)
    {
        return expr switch
        {
            BoundIntegerLiteralExpr => new BoundPrimitiveType("int"),
            BoundFloatLiteralExpr => new BoundPrimitiveType("float"),
            BoundStringLiteralExpr => new BoundPrimitiveType("string"),
            BoundBoolLiteralExpr => new BoundPrimitiveType("bool"),
            BoundNullLiteralExpr => new BoundPrimitiveType("null"),
            BoundVariableExpr v => v.Type,
            BoundGetExpr g => g.Type,
            BoundCallExpr c => AnalyzeCall(c, context),
            BoundBinaryExpr b => AnalyzeBinary(b, context),
            BoundUnaryExpr u => AnalyzeUnary(u, context),
            BoundAssignExpr a => AnalyzeAssign(a, context),
            BoundGroupingExpr g => AnalyzeExpr(g.Inner, context),
            BoundNewExpr n => AnalyzeNew(n, context),
            BoundErrorExpr => new BoundErrorType("error"),
            _ => new BoundErrorType("unknown")
        };
    }

    private BoundType AnalyzeBinary(BoundBinaryExpr expr, AnalysisContext context)
    {
        var left = AnalyzeExpr(expr.Left, context);
        var right = AnalyzeExpr(expr.Right, context);
        var op = expr.Operator.Type;

        // string + anything or anything + string -> string
        if (op == TokenType.Plus)
        {
            if (left is BoundPrimitiveType { Name: "string" } ||
                right is BoundPrimitiveType { Name: "string" })
                return new BoundPrimitiveType("string");
        }

        if (IsNumeric(left) && IsNumeric(right))
        {
            var result = WiderNumericType(left, right);
            if (op is TokenType.EqualEqual or TokenType.BangEqual or TokenType.Less or TokenType.Greater
                or TokenType.LessEqual or TokenType.GreaterEqual)
                return new BoundPrimitiveType("bool");
            return result;
        }

        if (op is TokenType.AmpAmp or TokenType.PipePipe)
        {
            if (left is BoundPrimitiveType { Name: "bool" } &&
                right is BoundPrimitiveType { Name: "bool" })
                return new BoundPrimitiveType("bool");
        }

        if (op is TokenType.EqualEqual or TokenType.BangEqual)
        {
            if (TypesAreEqual(left, right))
                return new BoundPrimitiveType("bool");
        }

        logger.Error(Phase,
            $"Operator '{expr.Operator.Lexeme}' cannot be applied to operands of type '{left}' and '{right}'",
            GetLocation(expr));
        return new BoundErrorType("operator_error");
    }

    private BoundType AnalyzeUnary(BoundUnaryExpr expr, AnalysisContext context)
    {
        var operand = AnalyzeExpr(expr.Operand, context);
        return expr.Operator.Type switch
        {
            TokenType.Bang when operand is BoundPrimitiveType { Name: "bool" } => new BoundPrimitiveType("bool"),
            TokenType.Minus when IsNumeric(operand) => operand,
            _ => LogAndReturnError(
                $"Operator '{expr.Operator.Lexeme}' cannot be applied to operand of type '{operand}'",
                GetLocation(expr))
        };
    }
    
    private BoundType AnalyzeAssign(BoundAssignExpr expr, AnalysisContext context)
    {
        var target = AnalyzeExpr(expr.Target, context);
        var value = AnalyzeExpr(expr.Value, context);

        if (!IsAssignableFrom(target, value))
            logger.Error(Phase,
                $"Cannot assign '{value}' to '{target}'.",
                GetLocation(expr));

        return target;
    }

    private BoundType AnalyzeCall(BoundCallExpr expr, AnalysisContext context)
    {
        var argTypes = expr.Arguments.Select(a => AnalyzeExpr(a, context)).ToList();

        if (expr.ResolvedTarget is null)
            return expr.Type; // unresolved, binder reported it

        ValidateArguments(expr.ResolvedTarget.Name, expr.ResolvedTarget.Parameters, argTypes, expr.Location);
        return expr.ResolvedTarget.ReturnType;
    }

    private BoundType AnalyzeNew(BoundNewExpr expr, AnalysisContext context)
    {
        var argTypes = expr.Arguments.Select(a => AnalyzeExpr(a, context)).ToList();
        
        if (expr.ResolvedConstructor is not null)
            ValidateArguments($"ctor '{expr.ResolvedConstructor.Name}'", expr.ResolvedConstructor.Parameters, argTypes, GetLocation(expr));
        else if (expr.Arguments.Count > 0 && expr.TargetType is BoundUserDefinedType)
            logger.Error(Phase, $"No constructor on '{expr.TargetType}' accepts {expr.Arguments.Count} arguments.", GetLocation(expr));
        return expr.Type;
    }

    private void ValidateArguments(string name, List<BoundParameter> parameters, List<BoundType> argTypes,
        TokenLocation? location)
    {
        if (parameters.Count != argTypes.Count)
        {
            logger.Error(Phase, $"Expected {parameters.Count} argument(s) for {name} but got {argTypes.Count}", location);
            return;
        }
        
        for (var i = 0; i < parameters.Count; i++)
            if (!IsAssignableFrom(parameters[i].Type, argTypes[i]))
                logger.Error(Phase, $"Expected argument {i + 1} of {name} to be of type {parameters[i].Type} but got {argTypes[i]}", location);
    }

    private static bool IsNumeric(BoundType type) => type is BoundPrimitiveType
    {
        Name: "ine" or "float" or "double"
    };

    private static BoundType WiderNumericType(BoundType a, BoundType b)
    {
        var order = new[] { "int", "float", "double" };
        var aIndex = Array.IndexOf(order, ((BoundPrimitiveType)a).Name);
        var bIndex = Array.IndexOf(order, ((BoundPrimitiveType)b).Name);
        return aIndex >= bIndex ? a : b;
    }

    private static bool TypesAreEqual(BoundType a, BoundType b) => a switch
    {
        BoundPrimitiveType pa when b is BoundPrimitiveType pb => pa.Name == pb.Name,
        BoundUserDefinedType ua when b is BoundUserDefinedType ub => ua.QualifiedName == ub.QualifiedName,
        _ => false
    };

    private static bool IsAssignableFrom(BoundType target, BoundType source)
    {
        if (TypesAreEqual(target, source))
            return true;
        if (IsNumeric(target) && IsNumeric(source))
            return true;
        if (target is BoundObjectType) return true;
        if (target is BoundErrorType || source is BoundErrorType) return true;
        return false;
    }

    private BoundType LogAndReturnError(string message, TokenLocation location)
    {
        logger.Error(Phase, message, location);
        return new BoundErrorType("error");
    }

    private static TokenLocation GetLocation(BoundStmt s)
    {
        return s.Location;
    }

    private static TokenLocation GetLocation(BoundExpr e)
    {
        return e.Location;
    }
}