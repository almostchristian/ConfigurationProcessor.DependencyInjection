namespace ConfigurationProcessor.SourceGeneration.Parsing;

/// <summary>
/// Represents a class that contains one or more methods to be code generated.
/// </summary>
public sealed class ServiceRegistrationClass
{
    /// <summary>
    /// List of methods to be code generated.
    /// </summary>
    public List<ServiceRegistrationMethod> Methods { get; } = new();

    /// <summary>
    /// The csharp keyword for the class declaration e.g. 'class' or 'record'.
    /// </summary>
    public string Keyword { get; init; } = string.Empty;

    /// <summary>
    /// The namespace of the class.
    /// </summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>
    /// The class name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The parent class if the class is nested.
    /// </summary>
    public ServiceRegistrationClass? ParentClass { get; set; }
}