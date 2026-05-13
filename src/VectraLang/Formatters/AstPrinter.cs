using System.Globalization;
using VectraLang.Ast.AstNodes;

namespace VectraLang.Formatters;

internal sealed class AstPrinter
{
    private int _indent;
    private const string IndentStr = "    ";

    private string Indent => string.Concat(Enumerable.Repeat(IndentStr, _indent));

    private void Block(string label, Action inner)
    {
        Console.WriteLine($"{Indent}{label}:");
        _indent++;
        inner();
        _indent--;
    }

    private void PrintValue(string value) => Console.WriteLine($"{Indent}{value}");

    public void Print(VectraFile file)
    {
        Block("VectraFile", () =>
        {
            PrintEnterDeclarations(file.EnterDeclarations);
            PrintSpace(file.Space);
        });
    }

    private void PrintEnterDeclarations(List<EnterDecl> enterDeclarations)
    {
        Block("EnterDeclarations", () =>
        {
            foreach (var enter in enterDeclarations)
                PrintValue(enter.QualifiedName);
        });
    }

    private void PrintSpace(SpaceDecl space)
    {
        Block(space.Name.Lexeme, () =>
        {
            foreach (var child in space.Children)
            {
                PrintSpace(child);
            }

            foreach (var declaration in space.Declarations)
            {
                PrintDeclaration(declaration);
            }
        });
    }

    private void PrintDeclaration(ITopLevelDecl declaration)
    {
        switch (declaration)
        {
            case ClassDecl cls:
                PrintClassDeclaration(cls);
                break;
            case InterfaceDecl iface:
                PrintInterfaceDeclaration(iface);
                break;
            case EnumDecl @enum:
                PrintEnumDeclaration(@enum);
                break;
        }
    }

    private void PrintClassDeclaration(ClassDecl cls)
    {
        Block($"class {cls.Name.Lexeme}: [{string.Join(", ", cls.Modifiers.Select(m => m.Lexeme))}]", () =>
        {
            foreach (var field in cls.Fields)
                PrintField(field);
            foreach (var p in cls.Properties)
                PrintProperty(p);
            foreach (var c in cls.Constructors)
                PrintCtor(c);
            foreach (var m in cls.Methods)
                PrintMethod(m);
        });
    }

    private void PrintInterfaceDeclaration(InterfaceDecl iface)
    {
        Block($"interface {iface.Name.Lexeme}", () =>
        {
            foreach (var m in iface.Methods)
                PrintMethodSignature(m);
        });
    }

    private void PrintEnumDeclaration(EnumDecl @enum)
    {
        Block($"enum {@enum.Name.Lexeme}", () =>
        {
            foreach (var variant in @enum.Variants)
                Block(variant.Name.Lexeme, () =>
                {
                    Block("fields", () =>
                    {
                        foreach (var arg in variant.Arguments)
                            PrintValue(PrintExpression(arg));
                    });
                    Block("overrides", () =>
                    {
                        foreach (var overrideMethod in variant.Overrides)
                            PrintMethod(overrideMethod);
                    });
                });
            foreach (var enumMethod in @enum.Methods)
                PrintMethod(enumMethod);
        });
    }

    private void PrintField(FieldDecl f)
    {
        PrintValue($"{f.Name.Lexeme}{(f.Initializer is null ? string.Empty : $" = {PrintExpression(f.Initializer)}")}");
    }

    private void PrintProperty(PropertyDecl p)
    {
        Block($"{p.Name.Lexeme}", () =>
        {
            if (p.Getter is not null)
            {
                PrintAccessor("get", p.Getter);
            }

            if (p.Setter is not null)
            {
                PrintAccessor("set", p.Setter);
            }
        });
    }

    private void PrintCtor(ConstructorDecl ctor)
    {
        Block("ctor", () =>
        {
            PrintValue($"Parameters: {string.Join(", ", ctor.Parameters.Select(PrintParameter))}");
            PrintStatement(ctor.Body);
        });
    }

    private void PrintMethod(MethodDecl m)
    {
        Block($"method: {m.Name.Lexeme}", () =>
        {
            PrintValue($"Parameters: {string.Join(", ", m.Parameters.Select(PrintParameter))}");
            PrintStatement(m.Body);
        });
    }

    private void PrintMethodSignature(MethodSignatureDecl m)
    {
        PrintValue($"{m.Name.Lexeme} ({string.Join(", ", m.Parameters.Select(PrintParameter))})");
    }

    private string PrintExpression(Expr e)
    {
        return e switch
        {
            BinaryExpr b => $"{PrintExpression(b.Left)} {b.Operator.Lexeme} {PrintExpression(b.Right)}",
            UnaryExpr u => $"{u.Operator.Lexeme}{PrintExpression(u.Right)}",
            GroupingExpr g => $"({PrintExpression(g.Inner)})",
            VariableExpr v => v.Name.Lexeme,
            AssignExpr a => $"{PrintExpression(a.Target)} = {PrintExpression(a.Value)}",
            CallExpr c => $"{PrintExpression(c.Callee)}({string.Join(", ", c.Arguments.Select(PrintExpression))})",
            GetExpr ge => $"{PrintExpression(ge.Object)}.{ge.Name.Lexeme}",
            OptionalGetExpr og => $"{PrintExpression(og.Object)}?.{og.Name.Lexeme}?",
            DestructureExpr d => $"{{{string.Join(", ", d.Names.Select(n => n.Lexeme))}}}",
            StringLiteralExpr s => $"\"{s.Value}\"",
            IntegerLiteralExpr i => i.Value.ToString(),
            BoolLiteralExpr b => b.Value.ToString(),
            FloatLiteralExpr f => f.Value.ToString(CultureInfo.InvariantCulture),
            NullLiteralExpr => "null",
            NewExpr n => $"new {n.TypeName.Lexeme}({string.Join(", ", n.Arguments.Select(PrintExpression))})",
            _ => throw new NotImplementedException($"Printing for expression type {e.GetType()} is not implemented")
        };
    }

    private void PrintAccessor(string accessor, Stmt s)
    {
        Block($"{accessor}", () => PrintStatement(s));
    }

    private string PrintParameter(ParameterNode p)
    {
        return $"{PrintTypeNode(p.Type)} {p.Name.Lexeme}";
    }

    private void PrintStatement(Stmt s)
    {
        switch (s)
        {
            case VarDeclStmt v:
                var type = PrintTypeNode(v.Type);
                PrintValue(
                    $"{type} {v.Name.Lexeme} = {(v.Initializer is not null ? PrintExpression(v.Initializer) : "null")}");
                break;
            case ExprStmt e:
                PrintValue(PrintExpression(e.Expression));
                break;
            case BlockStmt b:
                Block("block", () =>
                {
                    foreach (var stmt in b.Statements)
                        PrintStatement(stmt);
                });
                break;
            case IfStmt i:
                Block("if", () =>
                {
                    PrintValue(PrintExpression(i.Condition));
                    PrintStatement(i.ThenBranch);
                    if (i.ElseBranch is not null)
                    {
                        Block("else", () => { PrintStatement(i.ElseBranch); });
                    }
                });
                break;
            case WhileStmt w:
                Block("while", () =>
                {
                    PrintValue(PrintExpression(w.Condition));
                    PrintStatement(w.Body);
                });
                break;
            case ForStmt f:
                Block("for", () =>
                {
                    if (f.Initializer is not null)
                        PrintStatement(f.Initializer);
                    if (f.Condition is not null)
                        PrintValue(PrintExpression(f.Condition));
                    if (f.Increment is not null)
                        PrintValue(PrintExpression(f.Increment));
                    PrintStatement(f.Body);
                });
                break;
            case ReturnStmt r:
                PrintValue($"return {(r.Value is not null ? PrintExpression(r.Value) : string.Empty)}");
                break;
            case BreakStmt:
                PrintValue("break");
                break;
            case ContinueStmt:
                PrintValue("continue");
                break;
            default:
                throw new NotImplementedException($"Printing for statement type {s.GetType()} is not implemented");
        }
    }

    private string PrintTypeNode(TypeNode t)
    {
        return t switch
        {
            GenericTypeNode g => $"{g.TypeToken.Lexeme}<{string.Join(", ", g.TypeArguments.Select(PrintTypeNode))}>",
            InferredTypeNode => "let",
            PrimitiveTypeNode p => p.TypeToken.Lexeme,
            _ => throw new NotImplementedException($"Printing for type node type {t.GetType()} is not implemented")
        };
    }
}