namespace VectraLang.Bytecode;

public sealed class ImportTable
{
    private readonly List<ImportEntry> _imports = [];
    
    public ushort Add(string moduleName, ushort methodIndex)
    {
        var entry = new ImportEntry(moduleName, methodIndex);
        var existing = _imports.IndexOf(entry);
        if (existing >= 0) return (ushort)existing;
        _imports.Add(entry);
        return (ushort)(_imports.Count - 1);
    }

    public ImportEntry Get(ushort index) => _imports[index];
    public IReadOnlyList<ImportEntry> All => _imports;
}