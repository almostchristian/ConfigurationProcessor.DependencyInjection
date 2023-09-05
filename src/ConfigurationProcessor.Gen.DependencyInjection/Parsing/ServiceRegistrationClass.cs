namespace ConfigurationProcessor.Gen.DependencyInjection.Parsing;

internal sealed class ServiceRegistrationClass
{
    public List<ServiceRegistrationMethod> Methods { get; } = new();

    public string Keyword { get; init; } = string.Empty;

    public string Namespace { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public ServiceRegistrationClass? ParentClass { get; set; }
}