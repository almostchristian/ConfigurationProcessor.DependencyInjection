using System;
using System.Collections.Generic;
using System.Text;

namespace ConfigurationProcessor.DependencyInjection;

/// <summary>
/// Attribute for generating service registration.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class GenerateServiceRegistrationAttribute : Attribute
{
    /// <summary>
    /// The default configuration file name.
    /// </summary>
    public const string DefaultConfigurationFile = "appsettings.json";

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateServiceRegistrationAttribute"/> class.
    /// </summary>
    public GenerateServiceRegistrationAttribute(string configurationSection)
    {
        ConfigurationSection = configurationSection;
    }

    /// <summary>
    /// The configuration file.
    /// </summary>
    public string ConfigurationFile { get; set; } = DefaultConfigurationFile;

    /// <summary>
    /// Gets the configuration section.
    /// </summary>
    public string ConfigurationSection { get; }
}
