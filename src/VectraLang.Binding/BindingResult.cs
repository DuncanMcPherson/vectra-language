using VectraLang.Binding.Nodes;
using VectraLang.Binding.Scope;

namespace VectraLang.Binding;

public record BindingResult(BoundFile? BoundFile, BindingScope Scope, List<string> Errors)
{
    public bool IsSuccess => Errors.Count == 0;
}