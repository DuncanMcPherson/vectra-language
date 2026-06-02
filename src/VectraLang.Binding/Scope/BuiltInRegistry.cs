using VectraLang.Binding.Nodes;

namespace VectraLang.Binding.Scope;

public static class BuiltInRegistry
{
    private static readonly BoundObjectType ObjectType = new();
    private static readonly BoundPrimitiveType StringType = new("string");
    private static readonly BoundPrimitiveType IntType = new("int");
    private static readonly BoundPrimitiveType BoolType = new("bool");
    private static readonly BoundPrimitiveType VoidType = new("void");

    public static IEnumerable<BoundBuiltInFunction> GlobalFunctions =>
    [
        new("PrintLine", VoidType, [new("value", ObjectType, null!)]),
        new("Print", VoidType, [new("value", ObjectType, null!)]),
        new("ReadLine", StringType, [])
    ];

    public static IEnumerable<BoundBuiltInMethod> ObjectMethods =>
    [
        new("GetType", StringType, []),
        new("GetFullName", StringType, []),
        new("ToString", StringType, []),
        new("Equals", BoolType, [new("other", ObjectType, null!)]),
        new("GetHashCode", IntType, [])
    ];
}