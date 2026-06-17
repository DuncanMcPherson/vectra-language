namespace VectraLang.Bytecode;

public sealed record LoweredModule(
    string ModuleName,
    string Version,
    ConstantPool Constants,
    TypeTable Types,
    MethodTable Methods,
    ImportTable Imports);