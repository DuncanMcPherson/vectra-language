using VectraLang.Ast.AstNodes;

namespace VectraLang.Interpreter;

internal abstract record RuntimeValue
{
    public abstract string TypeName { get; }
    public abstract object? RawValue { get; }
}

internal sealed record IntValue(int Value) : RuntimeValue
{
    public override string TypeName => "int";
    public override object? RawValue => Value;
}

internal sealed record FloatValue(float Value) : RuntimeValue
{
    public override string TypeName => "float";
    public override object? RawValue => Value;
}

internal sealed record StringValue(string Value) : RuntimeValue
{
    public override string TypeName => "string";
    public override object? RawValue => Value;
}

internal sealed record BoolValue(bool Value) : RuntimeValue
{
    public override string TypeName => "bool";
    public override object? RawValue => Value;
}

internal sealed record NullValue : RuntimeValue
{
    public static readonly NullValue Instance = new();
    public override string TypeName => "null";
    public override object? RawValue => null;
}

internal sealed record VectraObject : RuntimeValue
{
    public ClassDecl Declaration { get; }
    public VectraEnvironment Fields { get; }
    public override string TypeName => Declaration.Name.Lexeme;
    public override object? RawValue => this;

    public VectraObject(ClassDecl declaration, VectraEnvironment fields)
    {
        Declaration = declaration;
        Fields = fields;
    }
    
    public RuntimeValue GetField(string name) => Fields.Get(name) as RuntimeValue ?? NullValue.Instance;
    public void SetField(string name, RuntimeValue value) => Fields.Assign(name, value);
}

internal sealed record VectraEnumVariant : RuntimeValue
{
    public EnumDecl Enum { get; }
    public EnumVariantNode Variant { get; }
    public VectraEnvironment Fields { get; }
    public override string TypeName => $"{Enum.Name.Lexeme}.{Variant.Name.Lexeme}";
    public override object? RawValue => this;

    public VectraEnumVariant(EnumDecl enumDecl, EnumVariantNode variant, VectraEnvironment fields)
    {
        Enum = enumDecl;
        Variant = variant;
        Fields = fields;
    }
}

internal abstract record CallableValue : RuntimeValue
{
    public abstract int Arity { get; }
    public abstract RuntimeValue Call(Interpreter interpreter, List<RuntimeValue> arguments);
    public override object? RawValue => this;
}

internal sealed record VectraMethod : CallableValue
{
    public MethodDecl Declaration { get; }
    public VectraEnvironment Closure { get; }
    public override string TypeName => "method";
    public override int Arity => Declaration.Parameters.Count;

    public VectraMethod(MethodDecl declaration, VectraEnvironment closure)
    {
        Declaration = declaration;
        Closure = closure;
    }

    public override RuntimeValue Call(Interpreter interpreter, List<RuntimeValue> arguments)
    {
        var env = Closure.CreateChild();
        for (var i = 0; i < Declaration.Parameters.Count; i++)
        {
            env.Define(Declaration.Parameters[i].Name.Lexeme, arguments[i]);
        }
        return interpreter.ExecuteBlock(Declaration.Body, env);
    }
}

internal sealed record VectraConstructor : CallableValue
{
    public ConstructorDecl Declaration { get; }
    public ClassDecl Owner { get; }
    public VectraEnvironment Closure { get; }
    public override string TypeName => "constructor";
    public override int Arity => Declaration.Parameters.Count;

    public VectraConstructor(ConstructorDecl declaration, ClassDecl owner, VectraEnvironment closure)
    {
        Declaration = declaration;
        Owner = owner;
        Closure = closure;
    }

    public override RuntimeValue Call(Interpreter interpreter, List<RuntimeValue> arguments)
    {
        var fields = new VectraEnvironment();
        var instance = new VectraObject(Owner, fields);
        
        foreach (var field in Owner.Fields)
            fields.Define(field.Name.Lexeme, interpreter.DefaultValue(field.Type));
        var env = Closure.CreateChild();
        env.Define("this", instance);
        
        for (var i = 0; i < Declaration.Parameters.Count; i++)
            env.Define(Declaration.Parameters[i].Name.Lexeme, arguments[i]);

        interpreter.ExecuteBlock(Declaration.Body, env);
        return instance;
    }
}

internal sealed record NativeFunction : CallableValue
{
    private readonly Func<List<RuntimeValue>, RuntimeValue> _fn;
    private readonly int _arity;

    public override string TypeName => "native";
    public override int Arity => _arity;

    public NativeFunction(int arity, Func<List<RuntimeValue>, RuntimeValue> fn)
    {
        _arity = arity;
        _fn = fn;
    }

    public override RuntimeValue Call(Interpreter interpreter, List<RuntimeValue> arguments) =>
        _fn(arguments);
}