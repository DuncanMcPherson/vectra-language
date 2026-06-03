using VectraLang.Binding.Nodes;
using VectraLang.Binding.Scope;

namespace VectraLang.Binding;

public record BindingResult(BoundNode? BoundRoot, BindingScope Scope, List<string> Errors)
{
    public bool IsSuccess => Errors.Count == 0;
}