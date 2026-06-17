namespace VectraLang.Bytecode;

public static class StdLib
{
    public const ushort ModuleIndex = 0;

    public static class Functions
    {
        public const ushort PrintLine = 0;
        public const ushort Print = 1;
        public const ushort ReadLine = 2;
    }
    
    public static class ObjectMethods
    {
        public const ushort GetTypeFn = 10;
        public const ushort GetFullName = 11;
        public const ushort ToStringFn = 12;
        public const ushort EqualsFn = 13;
        public const ushort GetHashCodeFn = 14;
    }

    public static bool TryResolveFunction(string name, out ushort methodIndex)
    {
        methodIndex = name switch
        {
            "PrintLine" => Functions.PrintLine,
            "Print" => Functions.Print,
            "ReadLine" => Functions.ReadLine,
            _ => ushort.MaxValue
        };
        return methodIndex != ushort.MaxValue;
    }
    
    public static bool TryResolveObjectMethod(string name, out ushort methodIndex)
    {
        methodIndex = name switch
        {
            "GetType" => ObjectMethods.GetTypeFn,
            "GetFullName" => ObjectMethods.GetFullName,
            "ToString" => ObjectMethods.ToStringFn,
            "Equals" => ObjectMethods.EqualsFn,
            "GetHashCode" => ObjectMethods.GetHashCodeFn,
            _ => ushort.MaxValue
        };
        return methodIndex != ushort.MaxValue;
    }
}