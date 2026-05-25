using Microsoft.CodeAnalysis;

internal sealed record AnalyzerRule(string Id, string Title, string? Url, bool Enabled, DiagnosticSeverity DefaultSeverity, DiagnosticSeverity? DefaultEffectiveSeverity);
