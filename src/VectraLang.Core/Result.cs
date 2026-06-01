namespace VectraLang.Core;

public class Result<T>
{
    public bool Success { get; }
    public T? Value { get; }

    public Result(bool success, T? value)
    {
        Success = success;
        Value = value;
    }
}