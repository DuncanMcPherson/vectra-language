using VectraLang.Ast.AstNodes;

namespace VectraLang.Binding.Nodes;

public abstract record BoundNode;

public sealed record BoundFile(
    BoundSpace Space,
    List<string> ResolvedImports) : BoundNode;

public sealed record BoundModule(
    string Name,
    List<BoundSpace> Spaces,
    bool IsExecutable,
    List<string> ResolvedImports) : BoundNode;

public sealed record BoundSpace(
    string QualifiedName,
    List<BoundTypeDecl> Declarations,
    List<BoundSpace> Children) : BoundNode;

public abstract record BoundTypeDecl(string QualifiedName) : BoundNode;

public sealed record BoundClass(
    string QualifiedName,
    List<BoundField> Fields,
    List<BoundProperty> Properties,
    List<BoundMethod> Methods,
    List<BoundConstructor> Constructors,
    ClassDecl Source) : BoundTypeDecl(QualifiedName);

public sealed record BoundInterface(
    string QualifiedName,
    List<BoundMethodSignature> Methods,
    InterfaceDecl Source) : BoundTypeDecl(QualifiedName);

public sealed record BoundEnum(
    string QualifiedName,
    List<BoundField> Fields,
    List<BoundMethod> Methods,
    List<BoundEnumVariant> Variants,
    EnumDecl Source) : BoundTypeDecl(QualifiedName);

public sealed record BoundEnumVariant(
    string Name,
    List<BoundExpr> Arguments,
    List<BoundMethod> Overrides,
    EnumVariantNode Source) : BoundNode;

public abstract record BoundMemberNode(string Name) : BoundNode;

public abstract record BoundCallable(string Name, List<BoundParameter> Parameters) : BoundMemberNode(Name);

public sealed record BoundField(string Name, BoundType Type, BoundExpr? Initializer, FieldDecl Source) : BoundMemberNode(Name);

public sealed record BoundProperty(string Name, BoundType Type, PropertyDecl Source, BoundPropertyGetter? Getter, BoundPropertySetter? Setter) : BoundMemberNode(Name);

public sealed record BoundParameter(string Name, BoundType Type, ParameterNode Source) : BoundNode;

public sealed record BoundMethod(string Name, BoundType ReturnType, List<BoundParameter> Parameters, BoundType ParentType, MethodDecl Source)
    : BoundCallable(Name, Parameters);

public sealed record BoundPropertyGetter(
    string Name,
    BoundType ReturnType,
    BoundType ParentType,
    PropertyDecl Source) : BoundCallable(Name, []);

public sealed record BoundPropertySetter(
    string Name,
    BoundType ValueType,
    BoundType ParentType,
    PropertyDecl Source) : BoundCallable(Name, [new("value", ValueType, null!)]);

public sealed record BoundMethodSignature(
    string Name,
    BoundType ReturnType,
    List<BoundParameter> Parameters,
    MethodSignatureDecl Source) : BoundNode;

public sealed record BoundConstructor(string Name, List<BoundParameter> Parameters, BoundType ParentType, ConstructorDecl Source)
    : BoundCallable(Name, Parameters);