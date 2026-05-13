using VectraLang.Ast.AstNodes;

namespace VectraLang.ModuleLoader;

public record MergedModule(
    string ModuleName,
    List<SpaceDecl> SpaceDecls,
    List<ITopLevelDecl> AllDeclarations);