using System.Reflection;
using ConfigurationProcessor.SourceGeneration.Parsing;
using ConfigurationProcessor.SourceGeneration.Utility;
using Microsoft.CodeAnalysis;
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
    /// <param name="currentAssembly"></param>
    /// <param name="generateConfigurationClasses"></param>
    /// <param name="references"></param>
    /// <param name="assemblyResolver"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static string Emit(IAssemblySymbol currentAssembly, IReadOnlyList<ServiceRegistrationClass> generateConfigurationClasses, List<Assembly> references, ReflectionPathAssemblyResolver assemblyResolver, CancellationToken cancellationToken)
    {
        var emitContext = new EmitContext(currentAssembly, generateConfigurationClasses.First().Namespace, references, assemblyResolver);

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

                if (configMethod.ConfigurationRoots.Length > 0)
                {
                    foreach (var configRoot in configMethod.ConfigurationRoots)
                    {
                        var prefix = $"{sectionName}:{configRoot}:";
                        var configValues = configMethod.ConfigurationValues
                            .Where(x => x.Key.StartsWith(prefix))
                            .ToList();
                        var configRootSectionName = $"config{configRoot.Replace(':', '_')}";
                        emitContext.Write($$"""

                            var {{configRootSectionName}} = {{configSectionVariableName}}.GetSection("{{configRoot}}");
                            """);
                        BuildMethods(emitContext, configValues, $"{sectionName}:{configRoot}", configMethod.TargetField!, configMethod.TargetTypeName!, configRootSectionName);
                    }
                }
                else
                {
                    BuildMethods(emitContext, configMethod.ConfigurationValues, sectionName, configMethod.TargetField!, configMethod.TargetTypeName!, configSectionVariableName);
                }

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

    private static void BuildMethods(EmitContext emitContext, IEnumerable<KeyValuePair<string, string?>> configurationValues, string sectionName, string targetExpression, string targetTypeName, string configSectionVariableName)
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
            targetTypeName,
            serviceCollectionParameterName);
    }
}