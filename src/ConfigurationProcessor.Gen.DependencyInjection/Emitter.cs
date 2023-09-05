using System.Reflection;
using ConfigurationProcessor.Gen.DependencyInjection.Parsing;
using ConfigurationProcessor.Gen.DependencyInjection.Utility;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.Gen.DependencyInjection;

internal class Emitter
{
    private GeneratorExecutionContext context;
    private readonly Action<Diagnostic> reportDiagnostic;

    public Emitter(GeneratorExecutionContext context, Action<Diagnostic> reportDiagnostic)
    {
        this.context = context;
        this.reportDiagnostic = reportDiagnostic;
    }

    public string Emit(IReadOnlyList<ServiceRegistrationClass> generateConfigurationClasses, CancellationToken cancellationToken)
    {
        var paths = context.Compilation.ExternalReferences.Select(x => x.Display!).ToList();
        var resolver = new PathAssemblyResolver(paths);
        var mlc = new MetadataLoadContext(resolver);

        var references = context.Compilation.ExternalReferences.Select(x => mlc.LoadFromAssemblyPath(x.Display!)).ToList();

        var emitContext = new EmitContext(generateConfigurationClasses.First().Namespace, references);

        foreach (var configClass in generateConfigurationClasses)
        {
            emitContext.Write($@"
namespace {configClass.Namespace}
{{
   static partial class {configClass.Name}
   {{");

            emitContext.IncreaseIndent();
            emitContext.IncreaseIndent();
            foreach (var configMethod in configClass.Methods)
            {
                var sectionName = configMethod.ConfigurationSectionName;
                var configFile = configMethod.FileName;

                var jsonFile = context.AdditionalFiles.FirstOrDefault(x => Path.GetFileName(x.Path) == configFile);
                if (jsonFile == null)
                {
                    Diag(DiagnosticDescriptors.ConfigurationFileNotFound, configMethod.Location, configMethod.FileName);
                    continue;
                }

                string configSectionVariableName = "servicesSection";

                emitContext.Write(
                    $@"[global::System.CodeDom.Compiler.GeneratedCodeAttribute(""ConfigurationProcessor.DependencyInjection.Generator"", ""0.1.0"")]
{configMethod.Modifiers} void {configMethod.Name}(this {configMethod.Arguments})
{{
   var {configSectionVariableName} = {configMethod.ConfigurationField}.GetSection(""{sectionName}"");
   if (!{configSectionVariableName}.Exists())
   {{
      return;
   }}");

                emitContext.IncreaseIndent();
                BuildMethods(emitContext, jsonFile.Path, sectionName, configMethod.ServiceCollectionField!, configSectionVariableName);
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

    private void BuildMethods(EmitContext emitContext, string jsonFilePath, string sectionName, string targetExpression, string configSectionVariableName)
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddJsonFile(jsonFilePath);
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

    private void Diag(DiagnosticDescriptor desc, Location? location, params object?[]? messageArgs)
    {
        reportDiagnostic(Diagnostic.Create(desc, location, messageArgs));
    }
}