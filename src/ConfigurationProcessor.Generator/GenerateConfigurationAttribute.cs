namespace ConfigurationProcessor;

/// <summary>
/// Attribute for generating service registration.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class GenerateConfigurationAttribute : Attribute
{
    /// <summary>
    /// The default configuration file name.
    /// </summary>
    public const string DefaultConfigurationFile = "appsettings.json";

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateConfigurationAttribute"/> class.
    /// </summary>
    public GenerateConfigurationAttribute(string configurationSection)
    {
        ConfigurationSection = configurationSection;
    }

    /// <summary>
    /// The configuration file.
    /// </summary>
    public string ConfigurationFile { get; set; } = DefaultConfigurationFile;

    /// <summary>
    /// The configuration path.
    /// </summary>
    public string? ConfigurationPath { get; set; }

    /// <summary>
    /// Sections to exclude.
    /// </summary>
    public string[] ExcludedSections { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Subsections that are treated separately.
    /// </summary>
    public string[] ExpandableSections { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Suffixes that can be ommitted.
    /// </summary>
    public string[] ImplicitSuffixes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets the configuration section.
    /// </summary>
    public string ConfigurationSection { get; }
}
