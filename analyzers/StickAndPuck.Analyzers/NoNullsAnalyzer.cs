using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StickAndPuck.Analyzers;

/// <summary>
/// Bans nullable types (SAP0001) and the null literal (SAP0002): absence is a Maybe or a closed type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoNullsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Any T?, int?, Action? and so on, wherever it is written.</summary>
    public static readonly DiagnosticDescriptor NullableType = new(
        "SAP0001",
        "Nullable type",
        "Nullable type '{0}': use Maybe<T>, a closed type or a non-optional default",
        "StickAndPuck",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>The null literal.</summary>
    public static readonly DiagnosticDescriptor NullLiteral = new(
        "SAP0002",
        "Null literal",
        "Null literal: use Maybe<T>, a closed type or a non-optional default",
        "StickAndPuck",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(NullableType, NullLiteral);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            c => c.ReportDiagnostic(Diagnostic.Create(NullableType, c.Node.GetLocation(), c.Node.ToString())),
            SyntaxKind.NullableType);
        context.RegisterSyntaxNodeAction(
            c => c.ReportDiagnostic(Diagnostic.Create(NullLiteral, c.Node.GetLocation())),
            SyntaxKind.NullLiteralExpression);
    }
}
