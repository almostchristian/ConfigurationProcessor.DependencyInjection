using ConfigurationProcessor.SourceGeneration.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using TestDummies;

namespace ConfigurationProcessor.SourceGeneration.UnitTests;

public class EmitterTests
{
    public static DummyDelegate DummyDelegateField = DelegateMembers.TestDelegate;

    [Fact]
    public void WithCustomRootObjectNotation_MapToExtensionMethodWithSingleStringParameterUsingStringValue_RegistersService()
    {
        TestConfig("""
            {
                "Root": { "SimpleString": "hello" }
            }
            """,
            """
            var configRoot = servicesSection.GetSection("Root");

            services.AddSimpleString(configRoot.GetValue<System.String>("SimpleString"));
            """,
            new[] { "Root" });
    }

    [Fact]
    public void WithObjectNotation_MapToExtensionMethodWithSingleStringParameterUsingStringValue_RegistersService()
    {
        TestConfig("""
            {
                "SimpleString": "hello"
            }
            """,
            @"services.AddSimpleString(servicesSection.GetValue<System.String>(""SimpleString""));");
    }

    [Fact]
    public void WithObjectNotation_MapStringArrayUsingArrayNotationWithSingleStringOverloadUsingArrayWithMultipleElements_RegistersWithArrayOverload()
    {
        TestConfig("""
            {
               "DummyString": [
                  "hello",
                  "world"
               ]
            }
            """,
            """
            var sectionDummyString = servicesSection.GetSection("DummyString");
            if (sectionDummyString.Exists())
            {
               services.AddDummyString(sectionDummyString.Get<System.String[]>());
            }
            """);
    }

    [Fact]
    public void WithObjectNotation_MapStringArrayUsingArrayNotationWithSingleStringOverloadUsingArrayWithSingleElement_RegistersWithArrayOverload()
    {
        TestConfig("""
            {
               "DummyString": [
                  "hello"
               ]
            }
            """,
            """
            var sectionDummyString = servicesSection.GetSection("DummyString");
            if (sectionDummyString.Exists())
            {
               services.AddDummyString(sectionDummyString.Get<System.String[]>());
            }
            """);
    }

    [Fact]
    public void WithObjectNotation_MapIntArrayDirectlyWithOverload_RegistersService()
    {
        TestConfig("""
            {
               "DummyArray": [
                  1,
                  2
               ]
            }
            """,
            """
            var sectionDummyArray = servicesSection.GetSection("DummyArray");
            if (sectionDummyArray.Exists())
            {
               services.AddDummyArray(sectionDummyArray.Get<System.Int32[]>());
            }
            """);
    }

    [Fact]
    public void WithObjectNotation_MapUsingStringDirectlyWithStringArrayOverload_RegistersWithSingleStringOverload()
    {
        TestConfig("""
            {
               "DummyString": "hello"
            }
            """,
            """
            services.AddDummyString(servicesSection.GetValue<System.String>("DummyString"));
            """);
    }

    [Fact]
    public void WithObjectNotation_MapToExtensionMethodWithSingleDelegateParameterDirectly_RegistersService()
    {
        TestConfig($$"""
            {
               "SimpleDelegate": "{{NameOf<DelegateMembers>()}}::{{nameof(DelegateMembers.TestDelegate)}}"
            }
            """,
            $$"""
            if (servicesSection.GetValue<string>("SimpleDelegate") == "{{NameOf<DelegateMembers>()}}::{{nameof(DelegateMembers.TestDelegate)}}")
            {
                services.AddSimpleDelegate({{NameOf<DelegateMembers>()}}.{{nameof(DelegateMembers.TestDelegate)}});
            }
            """);
    }

    [Theory]
    [InlineData("Time")]
    [InlineData("Time2")]
    public void WithObjectNotation_MapToExtensionMethodAcceptingConfigurationActionDelegate_GeneratesConfigurationActionBasedOnObject(string timeProperty)
    {
        TestConfig($$"""
            {
               "ConfigurationAction": {
                  "Name": "hello",
                  "Value": {
                     "{{timeProperty}}" : "13:00:10",
                     "Location": "http://www.google.com",
                     "ContextType": "{{NameOf<SimpleObject>()}}",
                     "OnError": "{{NameOf<EmitterTests>()}}::{{nameof(DummyDelegateField)}}"
                  }
               }
            }
            """,
            $$"""
            var sectionConfigurationAction = servicesSection.GetSection("ConfigurationAction");
            if (sectionConfigurationAction.Exists())
            {
               services.AddConfigurationAction(options =>
               {
                  options.Value = new TestDummies.ComplexObject.ChildValue();
                  if (sectionConfigurationAction.GetValue<string>("Value:ContextType") == "{{NameOf<SimpleObject>()}}")
                  {
                     options.Value.ContextType = typeof({{NameOf<SimpleObject>()}});
                  }
                  else
                  {
                     options.Value.ContextType = global::System.Type.GetType(sectionConfigurationAction.GetValue<string>("Value:ContextType"));
                  }

                  options.Value.Location = sectionConfigurationAction.GetValue<System.Uri>("Value:Location");
                  if (sectionConfigurationAction.GetValue<string>("Value:OnError") == "{{NameOf<EmitterTests>()}}::{{nameof(DummyDelegateField)}}")
                  {
                     options.Value.OnError = {{NameOf<EmitterTests>()}}.{{nameof(DummyDelegateField)}};
                  }

                  options.Value.{{timeProperty}} = sectionConfigurationAction.GetValue<System.TimeSpan?>("Value:{{timeProperty}}");
                  options.Name = sectionConfigurationAction.GetValue<System.String>("Name");
               });
            }
            """);
    }

    [Fact]
    public void WithObjectNotation_MapToExtensionMethodAcceptingConfigurationActionDelegate_CanCallExtensionMethodsWithSingleStringParameterForConfigurationObjectWithString()
    {
        TestConfig("""
            {
               "ConfigurationAction": {
                  "ConfigureName": "hello"
               }
            }
            """,
            """
            var sectionConfigurationAction = servicesSection.GetSection("ConfigurationAction");
            if (sectionConfigurationAction.Exists())
            {
               services.AddConfigurationAction(options =>
               {

                  options.AddConfigureName(sectionConfigurationAction.GetValue<System.String>("ConfigureName"));
               });
            }
            """);
    }


    [Fact]
    public void ConfigurationActionWithMethods()
    {
        TestConfig("""
            {
               "ConfigurationAction": {
                  "SetName": {
                        "Name": "hello"
                  }
               }
            }
            """,
            """
            var sectionConfigurationAction = servicesSection.GetSection("ConfigurationAction");
            if (sectionConfigurationAction.Exists())
            {
               services.AddConfigurationAction(options =>
               {

                  var sectionSetName = sectionConfigurationAction.GetSection("SetName");
                  if (sectionSetName.Exists())
                  {
                     options.SetName(sectionSetName.GetValue<System.String>("Name"));
                  }
               });
            }
            """);
    }

    [Fact]
    public void WithObjectNotation_MapToExtensionMethodWithSingleIntegerParameterUsingNumberValue_RegistersService()
    {
        TestConfig("""
            {
                "SimpleInt32": 42
            }
            """,
            @"services.AddSimpleInt32(servicesSection.GetValue<System.Int32>(""SimpleInt32""));");
    }

    [Fact]
    public void WithObjectNotation_MapToOverloadedExtensionMethodUsingNamedStringParameter_RegistersCorrectOverload()
    {
        TestConfig("""
            {
                "SimpleValue": { "StringValue": "hello" }
            }
            """,
            """
            var sectionSimpleValue = servicesSection.GetSection("SimpleValue");
            if (sectionSimpleValue.Exists())
            {
               services.AddSimpleValue(sectionSimpleValue.GetValue<System.String>("StringValue"));
            }
            """);
    }

    [Fact]
    public void WithObjectNotation_MapToOverloadedExtensionMethodUsingNamedIntParameter_RegistersCorrectOverload()
    {
        TestConfig("""
            {
                "SimpleValue": { "IntValue": 42 }
            }
            """,
            """
            var sectionSimpleValue = servicesSection.GetSection("SimpleValue");
            if (sectionSimpleValue.Exists())
            {
               services.AddSimpleValue(sectionSimpleValue.GetValue<System.Int32>("IntValue"));
            }
            """);
    }

    [Theory]
    [InlineData("SimpleType")]
    [InlineData("SimpleDelegate")]
    [InlineData("SimpleValue")]
    public void WithObjectNotation_MapToExtensionMethodWithNoParameter_RegistersService(string extensionName)
    {
        TestConfig($$"""
            {
               "{{extensionName}}": true
            }
            """,
            $$"""
            if (servicesSection.GetValue<bool>("{{extensionName}}"))
            {
               services.Add{{extensionName}}();
            }
            """);
    }

    [Fact]
    public void WithObjectNotation_MapToExtensionMethodAcceptingComplexObjectDirectly_InstantiatesObjectAndBindsValues()
    {
        TestConfig("""
            {
               "ComplexObject": {
                  "Name": "hello",
                  "Value": {
                        "Time" : "13:00:10"
                  }
               }
            }
            """,
            """
            var sectionComplexObject = servicesSection.GetSection("ComplexObject");
            if (sectionComplexObject.Exists())
            {
               services.AddComplexObject(sectionComplexObject.Get<TestDummies.ComplexObject>());
            }
            """);
    }

    [Theory]
    [InlineData("Time")]
    [InlineData("Time2")]
    public void WithObjectNotation_MapToExtensionMethodAcceptingConfigurationActionDelegate_CanCallExtensionMethodsForConfigurationObjectWithObjectNotation(string timeProperty)
    {
        TestConfig($$"""
            {
               "ConfigurationAction": {
                  "ConfigureName": {
                    "Name": "hello"
                  },
                  "ConfigureValue": {
                     "{{timeProperty}}" : "13:00:10"
                  }
               }
            }
            """,
            $$"""
            var sectionConfigurationAction = servicesSection.GetSection("ConfigurationAction");
            if (sectionConfigurationAction.Exists())
            {
               services.AddConfigurationAction(options =>
               {

                  var sectionConfigureName = sectionConfigurationAction.GetSection("ConfigureName");
                  if (sectionConfigureName.Exists())
                  {
                     options.AddConfigureName(sectionConfigureName.GetValue<System.String>("Name"));
                  }

                  var sectionConfigureValue = sectionConfigurationAction.GetSection("ConfigureValue");
                  if (sectionConfigureValue.Exists())
                  {
                     options.AddConfigureValue(options =>
                     {
                        options.{{timeProperty}} = sectionConfigureValue.GetValue<System.TimeSpan?>("{{timeProperty}}");
                     });
                  }
               });
            }
            """);
    }

    [Theory]
    [InlineData("DbConnection", "AddDbConnection", false)]
    [InlineData("ConfigureDbConnection", "ConfigureDbConnection", true)]
    public void WithObjectNotation_GivenConnectionString_SetsConnectionStringValue(string method, string expectedMethodCall, bool isLambda)
    {
        TestConfig($$"""
            {
                "{{method}}": {
                    "ConnectionString" : "abc"
                }
            }
            """,
            isLambda ?
            $$"""
            var sectionConfigureDbConnection = servicesSection.GetSection("ConfigureDbConnection");
            if (sectionConfigureDbConnection.Exists())
            {
               services.ConfigureDbConnection(options =>
               {
                  options.ConnectionString = sectionConfigureDbConnection.GetValue<System.String>("ConnectionString");
               });
            }
            """ :
            $$"""
            var section{{method}} = servicesSection.GetSection("{{method}}");
            if (section{{method}}.Exists())
            {
               services.{{expectedMethodCall}}(section{{method}}.GetValue<System.String>("ConnectionString"));
            }
            """);
    }


    [Fact]
    public void WithObjectNotation_CallExtensionMethodOnConfigurationObject_ExecutesInAlphabeticalOrderOfConfiguration()
    {
        TestConfig("""
            {
               "ConfigurationAction": {
                  "Name": "helloworld",
                  "Append<@x>": { "value": "1" },
                  "Append<@a>": { "value": "2" },
                  "Append<@b>": { "value": "3" },
                  "Append<@y>": { "value": "4" }
               }
            }
            """,
            """
            var sectionConfigurationAction = servicesSection.GetSection("ConfigurationAction");
            if (sectionConfigurationAction.Exists())
            {
               services.AddConfigurationAction(options =>
               {

                  var sectionAppend__a = sectionConfigurationAction.GetSection("Append<@a>");
                  if (sectionAppend__a.Exists())
                  {
                     options.Append<a>(sectionAppend__a.GetValue<System.String>("value"));
                  }

                  var sectionAppend__b = sectionConfigurationAction.GetSection("Append<@b>");
                  if (sectionAppend__b.Exists())
                  {
                     options.Append<b>(sectionAppend__b.GetValue<System.String>("value"));
                  }

                  var sectionAppend__x = sectionConfigurationAction.GetSection("Append<@x>");
                  if (sectionAppend__x.Exists())
                  {
                     options.Append<x>(sectionAppend__x.GetValue<System.String>("value"));
                  }

                  var sectionAppend__y = sectionConfigurationAction.GetSection("Append<@y>");
                  if (sectionAppend__y.Exists())
                  {
                     options.Append<y>(sectionAppend__y.GetValue<System.String>("value"));
                  }
                  options.Name = sectionConfigurationAction.GetValue<System.String>("Name");
               });
            }
            """,
            classDeclarations: """


            internal sealed class a { }
            internal sealed class b { }
            internal sealed class x { }
            internal sealed class y { }
            """);
    }

    private static void TestConfig([StringSyntax(StringSyntaxAttribute.Json)] string inputJsonFragment, string expectedCsharpFragment, string[]? roots = null, string? classDeclarations = null)
    {
        var inputJson = $$"""
            {
                "Services" : {{inputJsonFragment}}
            }
            """;

        var configurationValues = JsonConfigurationFileParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(inputJson)));
        var rc = new ServiceRegistrationClass
        {
            Name = "Test",
            Namespace = "TestApp",
            ParentClass = null,
            Keyword = "class",
        };
        rc.Methods.Add(new ServiceRegistrationMethod("Register", "this IServiceCollection services, IConfiguration configuration", "public partial void", configurationValues, roots ?? Array.Empty<string>(), "Services")
        {
            ConfigurationField = "configuration",
            TargetField = "services",
            TargetTypeName = "Microsoft.Extensions.DependencyInjection.IServiceCollection",
        });

        var assemblies = GetLoadedAssemblies();

        var generatedCsharp = Emitter.Emit(NSubstitute.Substitute.For<IAssemblySymbol>(), new[] { rc }, assemblies, new ReflectionPathAssemblyResolver(assemblies.Select(x => x.Location)), default);
        var expectedCsharp = $$"""
            // <auto-generated/>
            using Microsoft.Extensions.Configuration;
            using TestDummies;

            namespace TestApp
            {
               static partial class Test
               {
                  [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ConfigurationProcessor.Generator", "{{Emitter.VersionString}}")]
                  public partial void void Register(this IServiceCollection services, IConfiguration configuration)
                  {
                     var servicesSection = configuration.GetSection("Services");
                     if (!servicesSection.Exists())
                     {
                        return;
                     }

            {{IndentLines(expectedCsharpFragment, "         ")}}
                  }
               }
            }
            """;

        if (!string.IsNullOrEmpty(classDeclarations))
        {
            expectedCsharp = expectedCsharp + Environment.NewLine + classDeclarations;
        }

        Assert.Equal(expectedCsharp, generatedCsharp.Trim());
    }

    private static List<Assembly> GetLoadedAssemblies()
    {
        var dependencyContext = DependencyContext.Default;

        var query = from assemblyName in dependencyContext.RuntimeLibraries
                        .SelectMany(l => l.GetDefaultAssemblyNames(dependencyContext)).Distinct()
                    select assemblyName;

        return query.Select(Assembly.Load).ToList();
    }

    private static string IndentLines(string input, string indent)
    {
        var lines = input.Split(Environment.NewLine);
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                sb.AppendLine();
            }
            else
            {
                sb.Append(indent);
                sb.AppendLine(line);
            }
        }

        return sb.ToString().TrimEnd();
    }

    internal static string NameOf<T>() => typeof(T).FullName;
}
