using System.Reflection;
using ConfigurationProcessor.DependencyInjection.SourceGeneration.Parsing;
using ConfigurationProcessor.DependencyInjection.SourceGeneration.Utility;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.DependencyInjection.SourceGeneration;

internal class Emitter
{
    public static readonly string VersionString = typeof(Emitter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

    public string Emit(IReadOnlyList<ServiceRegistrationClass> generateConfigurationClasses, List<Assembly> references, CancellationToken cancellationToken)
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
                var sectionName = configMethod.ConfigurationSectionName;

                string configSectionVariableName = "servicesSection";

                emitContext.Write($$"""
                    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ConfigurationProcessor.DependencyInjection.Generator", "{{VersionString}}")]
                    {{configMethod.Modifiers}} void {{configMethod.Name}}({{configMethod.Arguments}})
                    {
                       var {{configSectionVariableName}} = {{configMethod.ConfigurationField}}.GetSection("{{sectionName}}");
                       if (!{{configSectionVariableName}}.Exists())
                       {
                          return;
                       }
                    """);

                emitContext.IncreaseIndent();
                BuildMethods(emitContext, configMethod.ConfigurationValues, sectionName, configMethod.ServiceCollectionField!, configSectionVariableName);
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

    private void BuildMethods(EmitContext emitContext, IEnumerable<KeyValuePair<string, string?>> configurationValues, string sectionName, string targetExpression, string configSectionVariableName)
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