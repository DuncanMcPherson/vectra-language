namespace VectraLang.Lowering.Helpers;

public class LoopContext
{
    public ushort ContinueTarget { get; private set; }
    public BackpatchList BreakJumps { get; } = new();
    public BackpatchList ContinueJumps { get; } = new();
    
    public void SetContinueTarget(ushort target)
    {
        ContinueTarget = target;
    }
}