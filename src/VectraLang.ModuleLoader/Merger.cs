using VectraLang.Ast.AstNodes;
using VectraLang.Core.Diagnostics;

namespace VectraLang.ModuleLoader;

public static class Merger
{
    public static MergedModule Merge(string moduleName, List<VectraFile> files, bool isExecutable, IVectraLogger logger)
    {
        var spaceMap = new Dictionary<string, SpaceDecl>();
        var allDeclarations = new List<ITopLevelDecl>();
        logger.Debug("Parse", $"Merging {files.Count} files into module '{moduleName}'");
        foreach (var file in files)
            MergeSpace(file.Space, spaceMap, allDeclarations, logger);
        
        // TODO: preserve the enter declarations somehow
        return new MergedModule(moduleName, spaceMap.Values.ToList(), allDeclarations, isExecutable, []);
    }

    private static SpaceDecl MergeSpace(SpaceDecl space, Dictionary<string, SpaceDecl>? spaceMap,
        List<ITopLevelDecl> allDeclarations, IVectraLogger logger)
    {
        SpaceDecl? existing = null;
        spaceMap?.TryGetValue(space.QualifiedName, out existing);

        if (existing is null)
        {
            logger.Debug("Parse", $"'{space.QualifiedName}' not found. Merging...");
            // new space - Register it
            if (spaceMap is not null)
                spaceMap[space.QualifiedName] = space;
            allDeclarations.AddRange(space.Declarations);

            foreach (var child in space.Children.ToList())
            {
                logger.Debug("Parse", $"Merging child '{child.Name.Lexeme}' into '{space.QualifiedName}'");
                space.Children.Remove(child);
                var mergedChild = MergeSpace(child, null, allDeclarations, logger);
                if (!space.Children.Contains(mergedChild))
                    space.Children.Add(mergedChild);
            }
            
            return space;
        }

        logger.Debug("Parse", $"Found existing space '{space.QualifiedName}'. Merging...");
        foreach (var decl in space.Declarations)
        {
            existing.AddDeclaration(decl);
            allDeclarations.Add(decl);
        }

        foreach (var incomingChild in space.Children)
        {
            var existingChild = existing.Children.FirstOrDefault(c => c.Name.Lexeme == space.Name.Lexeme);

            if (existingChild is null)
            {
                logger.Debug("Parse", $"Child '{incomingChild.Name.Lexeme}' not found in existing space. Adding...");
                var mergedChild = MergeSpace(incomingChild, null, allDeclarations, logger);
                existing.AddChild(mergedChild);
            }
            else
            {
                logger.Debug("Parse", $"Child '{incomingChild.Name.Lexeme}' already exists in existing space. Merging...");
                MergeSpace(incomingChild, null, allDeclarations, logger);
            }
        }
        
        return existing;
    }
}