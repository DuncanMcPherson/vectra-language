using VectraLang.Core;

namespace VectraLang.Interpreter;

public sealed class RuntimeException(string message, TokenLocation? location = null) : Exception(message)
{
    public TokenLocation? Location { get; } = location;
}