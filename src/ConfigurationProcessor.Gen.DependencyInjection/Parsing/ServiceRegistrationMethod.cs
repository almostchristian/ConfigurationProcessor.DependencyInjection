using Microsoft.CodeAnalysis;

namespace ConfigurationProcessor.Gen.DependencyInjection.Parsing;

internal sealed record class ServiceRegistrationMethod(string Name, string Arguments, string Modifiers, string FileName, string ConfigurationSectionName, Location Location)
{
    public string UniqueName { get; set; } = string.Empty;

    public string? ServiceCollectionField { get; set; }

    public string? ConfigurationField { get; set; }
}