using VectraLang.Ast.AstNodes;

namespace VectraLang.ModuleLoader;

public static class Merger
{
    public static MergedModule Merge(string moduleName, List<VectraFile> files)
    {
        var spaces = new List<SpaceDecl>();
        var allDeclarations = new List<ITopLevelDecl>();

        foreach (var file in files)
        {
            spaces.Add(file.Space);
            allDeclarations.AddRange(file.Space.Declarations);
            CollectFromChildren(file.Space, allDeclarations);
        }
        
        return new MergedModule(moduleName, spaces, allDeclarations);
    }

    private static void CollectFromChildren(SpaceDecl space, List<ITopLevelDecl> allDeclarations)
    {
        foreach (var child in space.Children)
        {
            allDeclarations.AddRange(child.Declarations);
            CollectFromChildren(child, allDeclarations);
        }
    }
}