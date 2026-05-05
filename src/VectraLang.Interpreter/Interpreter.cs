using VectraLang.Ast.AstNodes;

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
                    _globals.Define(e.Name.Lexeme, e);
                    RegisterEnumVariants(e);
                    break;
                case InterfaceDecl:
                    break;
            }
        }
    }

    private void RegisterEnumVariants(EnumDecl e)
    {
        foreach (var variant in e.Variants)
        {
            var fields = new VectraEnvironment();
            var instance = new VectraEnumVariant(e, variant, fields);
            _globals.Define($"{e.Name.Lexeme}.{variant.Name.Lexeme}", instance);
        }
    }

    private bool TryFindEntryPoint(VectraFile file, out VectraMethod? main)
    {
        
    }
}