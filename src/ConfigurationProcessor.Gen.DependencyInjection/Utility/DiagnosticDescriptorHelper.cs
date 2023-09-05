using Microsoft.CodeAnalysis;

namespace ConfigurationProcessor.Gen.DependencyInjection.Utility;

/// <summary>
/// Helper methods for creating <see cref="DiagnosticDescriptor"/> instances.
/// </summary>
internal static class DiagnosticDescriptorHelper
{
    public static DiagnosticDescriptor Create(
            string id,
            string title,
            string messageFormat,
            string category,
            DiagnosticSeverity defaultSeverity,
            bool isEnabledByDefault,
            string? description = null,
            params string[] customTags)
    {
        string helpLink = $"https://learn.microsoft.com/dotnet/fundamentals/syslib-diagnostics/{id.ToLowerInvariant()}.md";

        return new DiagnosticDescriptor(id, title, messageFormat, category, defaultSeverity, isEnabledByDefault, description, helpLink, customTags);
    }
}