using Microsoft.CodeAnalysis;

internal sealed record AnalyzerConfiguration(string Id, string[] Comments, DiagnosticSeverity? Severity, string[] Options);
