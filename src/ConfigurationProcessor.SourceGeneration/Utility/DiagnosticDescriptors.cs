using Microsoft.CodeAnalysis;

namespace ConfigurationProcessor.SourceGeneration.Utility;

internal static class DiagnosticDescriptors
{
    private const string Category = "ConfigurationProcessor";

    public static DiagnosticDescriptor InvalidGenerateConfigurationMethodName { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1001",
        title: "Configuration methods cannot start with _",
        messageFormat: "Configuration methods name '{0}' is invalid.",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidGenerateConfigurationMethodParameterName { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1003",
        title: "Unknown parameter name",
        messageFormat: "Unknown parameter name '{0}'",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor GenerateConfigurationMethodMustReturnVoid { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1007",
        title: "Configuration method must return void",
        messageFormat: "Configuration method must return void",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MissingGenerateConfigurationArgument { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1008",
        title: "Missing IServiceCollection argument",
        messageFormat: "Missing IServiceCollection argument",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor GenerateConfigurationMethodShouldBeStatic { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1009",
        title: "Configuration method should be static",
        messageFormat: "Configuration method should be static",
        category: Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor GenerateConfigurationMethodMustBePartial { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1010",
        title: "Configuration method should be partial",
        messageFormat: "Configuration method should be partial",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor GenerateConfigurationMethodIsGeneric { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1011",
        title: "Generic configuration method is not supported",
        messageFormat: "Generic configuration method is not supported",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor GenerateConfigurationMethodHasBody { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1016",
        title: "Configuration method has a body",
        messageFormat: "Configuration method has a body",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MissingConfigurationSection { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1017",
        title: "Configuration section not found",
        messageFormat: "Configuration section {0} not found in file {1}.",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MissingConfigurationParameter { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1018",
        title: "Missing IConfiguration parameter/field",
        messageFormat: "Missing IConfiguration parameter/field",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MissingServiceCollectionParameter { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1019",
        title: "Missing IServiceCollection parameter/field",
        messageFormat: "Missing IServiceCollection paramter/field",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MultipleServiceCollectionParameter { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1020",
        title: "Multiple IServiceCollection parameters/fields",
        messageFormat: "Multiple IServiceCollection parameters/fields",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MultipleConfigurationParameter { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1021",
        title: "Multiple IConfiguration parameters/fields",
        messageFormat: "Multiple IConfiguration parameters/fields",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor GenerateConfigurationUnsupportedLanguageVersion { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1026",
        title: "Unsupported Language Version",
        messageFormat: "Only CSharp 8.0 and above is supported.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ConfigurationFileNotFound { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1027",
        title: "Configuration file not found.",
        messageFormat: "Configuration file '{0}' not found.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TopLevelClassNotSupported { get; } = DiagnosticDescriptorHelper.Create(
        id: "CPGEN1028",
        title: "Top level class is not supported.",
        messageFormat: "Top level class is not valid.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}