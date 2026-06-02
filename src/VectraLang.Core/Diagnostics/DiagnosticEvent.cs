namespace VectraLang.Core.Diagnostics;

public record DiagnosticEvent(
    DiagnosticSeverity Severity,
    string Phase,
    string Message,
    TokenLocation? Location = null);