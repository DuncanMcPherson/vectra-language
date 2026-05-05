namespace VectraLang.Interpreter;

public static class ObjectMethodsRegistry
{
    internal static readonly Dictionary<string, Func<RuntimeValue, RuntimeValue>> Methods = new()
    {
        ["GetType"] = receiver => new NativeFunction(0, _ => new StringValue(receiver.TypeName)),
        ["GetFullName"] = receiver => new NativeFunction(0, _ =>
        {
            if (receiver is VectraObject obj)
                return new StringValue(obj.Declaration.GetFullName());
            return new StringValue(receiver.TypeName);
        }),
        ["ToString"] = receiver => new NativeFunction(0, _ => new StringValue(receiver.RawValue?.ToString() ?? "null")),
        ["Equals"] = receiver => new NativeFunction(1, args =>
        {
            if (receiver is VectraObject obj && args[0] is VectraObject other)
                return new BoolValue(ReferenceEquals(obj, other));
            return new BoolValue(receiver.RawValue?.Equals(args[0].RawValue) ?? false);
        }),
        ["GetHashCode"] = receiver => new NativeFunction(0, _ => new IntValue(receiver.RawValue?.GetHashCode() ?? 0)),
    };
}