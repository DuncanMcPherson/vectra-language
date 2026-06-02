namespace VectraLang.Core.Diagnostics;

public interface IVectraLogger
{
    void Log(DiagnosticEvent evt);

    void Debug(string phase, string message, TokenLocation? location = null)
        => Log(new DiagnosticEvent(DiagnosticSeverity.Debug, phase, message, location));
    void Info(string phase, string message, TokenLocation? location = null)
        => Log(new DiagnosticEvent(DiagnosticSeverity.Info, phase, message, location));
    void Warning(string phase, string message, TokenLocation? location = null)
        => Log(new DiagnosticEvent(DiagnosticSeverity.Warning, phase, message, location));
    void Error(string phase, string message, TokenLocation? location = null)
        => Log(new DiagnosticEvent(DiagnosticSeverity.Error, phase, message, location));
}