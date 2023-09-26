using Microsoft.CodeAnalysis;

namespace ConfigurationProcessor.SourceGeneration.Parsing;

/// <summary>
/// Represents a method to be code generated.
/// </summary>
/// <param name="Name">The method name.</param>
/// <param name="Arguments"></param>
/// <param name="Modifiers"></param>
/// <param name="ConfigurationValues"></param>
/// <param name="ConfigurationSectionName"></param>
public sealed record class ServiceRegistrationMethod(string Name, string Arguments, string Modifiers, IEnumerable<KeyValuePair<string, string?>> ConfigurationValues, string ConfigurationSectionName)
{
    /// <summary>
    /// The unique method name.
    /// </summary>
    public string UniqueName { get; set; } = string.Empty;

    /// <summary>
    /// The target field expression.
    /// </summary>
    public string? TargetField { get; set; }

    /// <summary>
    /// The target type name.
    /// </summary>
    public string? TargetTypeName { get; set; }

    /// <summary>
    /// The configuration field expression.
    /// </summary>
    public string? ConfigurationField { get; set; }

    /// <summary>
    /// The implicit suffixes.
    /// </summary>
    public string[]? ImplicitSuffixes { get; set; }
}