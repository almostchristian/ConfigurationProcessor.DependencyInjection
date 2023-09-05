using Microsoft.CodeAnalysis;

namespace ConfigurationProcessor.DependencyInjection.SourceGeneration.Parsing;

internal sealed record class ServiceRegistrationMethod(string Name, string Arguments, string Modifiers, IEnumerable<KeyValuePair<string, string?>> ConfigurationValues, string ConfigurationSectionName)
{
    public string UniqueName { get; set; } = string.Empty;

    public string? ServiceCollectionField { get; set; }

    public string? ConfigurationField { get; set; }
}