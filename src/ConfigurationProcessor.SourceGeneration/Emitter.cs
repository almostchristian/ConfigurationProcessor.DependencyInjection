using System.Reflection;
using ConfigurationProcessor.SourceGeneration.Parsing;
using ConfigurationProcessor.SourceGeneration.Utility;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.SourceGeneration;

/// <summary>
/// Generates code.
/// </summary>
public static class Emitter
{
    /// <summary>
    /// The version string.
    /// </summary>
    public static readonly string VersionString = typeof(Emitter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

    /// <summary>
    /// Generates code from service class registrations.
    /// </summary>
    /// <param name="generateConfigurationClasses"></param>
    /// <param name="references"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static string Emit(IReadOnlyList<ServiceRegistrationClass> generateConfigurationClasses, List<Assembly> references, CancellationToken cancellationToken)
    {
        var emitContext = new EmitContext(generateConfigurationClasses.First().Namespace, references);

        foreach (var configClass in generateConfigurationClasses)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            emitContext.Write($$"""
                namespace {{configClass.Namespace}}
                {
                   static partial class {{configClass.Name}}
                   {
                """);

            emitContext.IncreaseIndent();
            emitContext.IncreaseIndent();
            foreach (var configMethod in configClass.Methods)
            {
                emitContext.ImplicitSuffixes = configMethod.ImplicitSuffixes;
                var sectionName = configMethod.ConfigurationSectionName;

                string configSectionVariableName = "servicesSection";

                emitContext.Write($$"""
                    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ConfigurationProcessor.Generator", "{{VersionString}}")]
                    {{configMethod.Modifiers}} void {{configMethod.Name}}({{configMethod.Arguments}})
                    {
                       var {{configSectionVariableName}} = {{configMethod.ConfigurationField}}.GetSection("{{sectionName}}");
                       if (!{{configSectionVariableName}}.Exists())
                       {
                          return;
                       }
                    """);

                emitContext.IncreaseIndent();
                BuildMethods(emitContext, configMethod.ConfigurationValues, sectionName, configMethod.TargetField!, configSectionVariableName);
                emitContext.DecreaseIndent();
                emitContext.Write("}");
            }

            emitContext.DecreaseIndent();
            emitContext.DecreaseIndent();

            emitContext.Write(@"   }
}
");
        }

        return emitContext.ToString();
    }

    private static void BuildMethods(EmitContext emitContext, IEnumerable<KeyValuePair<string, string?>> configurationValues, string sectionName, string targetExpression, string configSectionVariableName)
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(configurationValues);
        var config = configBuilder.Build();

        var directive = config.GetSection(sectionName);

        string serviceCollectionParameterName = targetExpression;
        emitContext.EmitValues(
            config,
            directive,
            sectionName,
            configSectionVariableName,
            Parser.ServiceCollectionTypeName,
            serviceCollectionParameterName);
    }
}