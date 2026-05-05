namespace VectraLang.Interpreter;

internal sealed class ReturnException(RuntimeValue value) : Exception
{
    public RuntimeValue Value { get; } = value;
}