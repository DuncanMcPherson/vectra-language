namespace VectraLang.Ast.Tokens;

public static class TokenExtensions
{
    public static string FormatLocation(this Token token)
    {
        return $"{token.Location.FileName}:{token.Location.StartLine}:{token.Location.StartColumn}";
    }
}