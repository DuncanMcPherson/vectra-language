namespace VectraLang.Binding.Nodes;

public record BoundPackage(string PackageName, string Version, List<BoundModule> Modules) : BoundNode;