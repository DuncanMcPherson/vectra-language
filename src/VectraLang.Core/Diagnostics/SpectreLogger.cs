using Spectre.Console;

namespace VectraLang.Core.Diagnostics;

public class SpectreLogger(DiagnosticSeverity minimumSeverity = DiagnosticSeverity.Info) : IVectraLogger
{
    public void Log(DiagnosticEvent evt)
    {
        if (evt.Severity < minimumSeverity) return;

        var (color, label) = evt.Severity switch
        {
            DiagnosticSeverity.Debug => ("grey", "DBG"),
            DiagnosticSeverity.Info => ("white", "INF"),
            DiagnosticSeverity.Warning => ("yellow", "WRN"),
            DiagnosticSeverity.Error => ("red", "ERR"),
            _ => ("white", "???")
        };
        
        var location = evt.Location is not null
            ? $" [grey]({evt.Location.FileName} {evt.Location.StartLine}:{evt.Location.StartColumn})[/]"
            : "";
        
        AnsiConsole.MarkupLine($"[{color}][[{label}]][/] [grey][[{evt.Phase}]][/] {evt.Message}{location}");
    }
}