namespace VectraLang.Ast.Tokens;

public sealed record TokenLocation(
    string FileName,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn);