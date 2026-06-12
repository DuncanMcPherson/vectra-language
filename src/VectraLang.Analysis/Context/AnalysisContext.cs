using VectraLang.Binding.Nodes;

namespace VectraLang.Analysis.Context;

public class AnalysisContext
{
    public BoundType? ExpectedReturnType { get; set; }
    public int LoopDepth { get; set; }
    public bool HasReturn { get; set; }
    
    public bool IsInsideLoop => LoopDepth > 0;

    public AnalysisContext EnterLoop()
    {
        LoopDepth++;
        return this;
    }
    
    public AnalysisContext ExitLoop()
    {
        LoopDepth--;
        return this;
    }

    public AnalysisContext Clone() => new()
    {
        ExpectedReturnType = ExpectedReturnType,
        LoopDepth = LoopDepth,
        HasReturn = HasReturn
    };
}