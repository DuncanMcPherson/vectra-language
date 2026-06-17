namespace VectraLang.Lowering.Helpers;

public class BackpatchList
{
    private readonly List<int> _pendingJumps = [];
    public void Add(int instructionIndex) => _pendingJumps.Add(instructionIndex);

    public void PatchAll(List<ushort> instructions)
    {
        var target = (ushort)instructions.Count;
        foreach (var idx in _pendingJumps)
            instructions[idx] = target;
        _pendingJumps.Clear();
    }

    public void PatchOne(int instructionIndex, List<ushort> instructions, ushort target)
    {
        instructions[instructionIndex] = target;
    }
}