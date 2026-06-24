using VectraLang.Ast.AstNodes;

namespace VectraLang.ModuleLoader;

public record MergedModule(
    string ModuleName,
    bool IsExecutable,
    List<VectraFile> Files,
    List<string> Dependencies);