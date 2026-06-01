using VectraLang.Ast.AstNodes;

namespace VectraLang.Binding.Nodes;

public abstract record BoundNode;

public sealed record BoundFile(
    BoundSpace Space,
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
    EnumDecl Source) : BoundTypeDecl(QualifiedName);

public abstract record BoundMemberNode(string Name) : BoundNode;

public abstract record BoundCallable(string Name, List<BoundParameter> Parameters) : BoundMemberNode(Name);

public sealed record BoundField(string Name, BoundType Type, FieldDecl Source) : BoundMemberNode(Name);

public sealed record BoundProperty(string Name, BoundType Type, PropertyDecl Source) : BoundMemberNode(Name);

public sealed record BoundParameter(string Name, BoundType Type, ParameterNode Source) : BoundNode;

public sealed record BoundMethod(string Name, BoundType ReturnType, List<BoundParameter> Parameters, MethodDecl Source)
    : BoundCallable(Name, Parameters);

public sealed record BoundMethodSignature(
    string Name,
    BoundType ReturnType,
    List<BoundParameter> Parameters,
    MethodSignatureDecl Source) : BoundNode;

public sealed record BoundConstructor(string Name, List<BoundParameter> Parameters, ConstructorDecl Source)
    : BoundCallable(Name, Parameters);