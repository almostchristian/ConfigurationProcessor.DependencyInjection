using ConfigurationProcessor.SourceGeneration.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using TestDummies;

namespace ConfigurationProcessor.SourceGeneration.UnitTests;

public class EmitterTests
{
    public static DummyDelegate DummyDelegateField = DelegateMembers.TestDelegate;

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

    private static void TestConfig([StringSyntax(StringSyntaxAttribute.Json)] string inputJsonFragment, string expectedCsharpFragment)
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
        rc.Methods.Add(new ServiceRegistrationMethod("Register", "this IServiceCollection services, IConfiguration configuration", "public partial void", configurationValues, "Services")
        {
            ConfigurationField = "configuration",
            TargetField = "services",
        });

        var assemblies = GetLoadedAssemblies();

        var generatedCsharp = Emitter.Emit(new[] { rc }, assemblies, default);
        var expectedCsharp = $$"""
            // <auto-generated/>
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
