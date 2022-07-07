// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using TestDummies;
using static ConfigurationProcessor.DependencyInjection.UnitTests.Support.Extensions;

namespace ConfigurationProcessor.DependencyInjection.UnitTests
{
   public abstract class ConfigurationBuilderTestsBase
   {
#pragma warning disable SA1401 // Fields should be private
      public static DummyDelegate DummyDelegateField = DelegateMembers.TestDelegate;

      public static DummyDelegate DummyDelegateProperty { get; } = DelegateMembers.TestDelegate;

      [Fact]
      public void WithObjectNotation_UsingConfigureMethod_ConfiguresObject()
      {
         var json = $@"
{{
   'Configure<{NameOf<SimpleObject>()}>' : {{
      'PropertyB': 'hello'
   }}
}}";

         var sp = BuildFromJson(json);
         var option = sp.GetRequiredService<IOptions<SimpleObject>>();
         Assert.Equal("hello", option.Value.PropertyB);
      }

      [Fact]
      public void WithArrayNotation_AddMultipleServicesUsingObjectStringMix_RegistersServices()
      {
         var json = @$"
[
   'DummyGeneric`{NameOf<DummyTestClass>()}',
   {{
      'Name': 'AddDummyDelegate',
      'DummyDelegate': '{NameOf<DelegateMembers>()}::{nameof(DelegateMembers.TestDelegate)}'
   }},
   {{
      'Name': 'ConfigurationAction',
      'SetName': {{
            'Name': 'hello'
      }}
   }},
   {{
      'Name': 'ConfigurationAction2',
      'PropertyA': 42,
      'PropertyB': 'hello',
      'PropertyC': false
   }}
]";

         var serviceCollection = ProcessJson(json);
         Assert.Collection(
             serviceCollection.Take(2),
             sd => Assert.Equal(typeof(DummyTestClass), sd.ServiceType),
             sd => Assert.Equal(typeof(DummyDelegate), sd.ServiceType));

         var sp = serviceCollection.BuildServiceProvider();
         var option = sp.GetService<IOptions<ComplexObject>>();

         Assert.NotNull(option);
         Assert.Equal("hello", option.Value.Name);

         var option2 = sp.GetService<IOptions<SimpleObject>>();

         Assert.NotNull(option2);
         Assert.Equal("hello", option2.Value.PropertyB);
         Assert.False(option2.Value.PropertyC);
      }

      [Fact]
      public void WithArrayNotation_MapWithUnknownExtensionMethod_ThrowsMissingMethodExceptionWithListOfAllExtensionMethods()
      {
         var json = @$"
[{{
   'Name': 'AddDummyDelegxxx',
   'DummyDelegate': '{NameOf<DelegateMembers>()}::{nameof(DelegateMembers.TestDelegate)}'
}}]";

         var exception = Assert.Throws<MissingMethodException>(() => ProcessJson(json));

         Assert.Contains("AddDummyDelegate", exception.Message);
      }

      [Fact]
      public void WithObjectNotation_MapExtensionMethodWithNonMatchingNamedParameter_ThrowsMissingMethodExceptionWithListOfAllMatchingOverloads()
      {
         var json = @$"
{{
   'SimpleString': {{
      'DummyDelegate': '{NameOf<DelegateMembers>()}::{nameof(DelegateMembers.TestDelegate)}'
   }}
}}";

         var exception = Assert.Throws<MissingMethodException>(() => ProcessJson(json));

         Assert.Contains("AddSimpleString(value)", exception.Message);
      }

      [Fact]
      public void WithArrayNotation_MapWithExtensionMethodWithIncorrectParameters_ThrowsMissingMethodException()
      {
         var json = @$"
[{{
   'Name': 'AddDummyDelegate',
   'DummyDelegatexxx': '{NameOf<DelegateMembers>()}::{nameof(DelegateMembers.TestDelegate)}'
}}]";

         Assert.Throws<MissingMethodException>(() => ProcessJson(json));
      }

      [Fact]
      public void WithArrayNotation_MapStaticMethodToDelegateUsingObjectNotation_RegistersService()
      {
         var json = @$"
[{{
   'Name': 'AddDummyDelegate',
   'DummyDelegate': '{NameOf<DelegateMembers>()}::{nameof(DelegateMembers.TestDelegate)}'
}}]";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(DummyDelegate), sd.ServiceType));
      }

      [Fact]
      public void WithArrayNotation_MapStaticDelegatePropertyToDelegateUsingObjectNotation_RegistersService()
      {
         var json = @$"
[{{
   'Name': 'AddDummyDelegate',
   'DummyDelegate': '{NameOf<ConfigurationBuilderTestsBase>()}::{nameof(DummyDelegateProperty)}'
}}]";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(DummyDelegate), sd.ServiceType));
      }

      [Fact]
      public void WithArrayNotation_MapStaticDelegateFieldToDelegateUsingObjectNotation_RegistersService()
      {
         var json = @$"
[{{
   'Name': 'AddDummyDelegate',
   'DummyDelegate': '{NameOf<ConfigurationBuilderTestsBase>()}::{nameof(DummyDelegateField)}'
}}]";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(DummyDelegate), sd.ServiceType));
      }

      [Fact]
      public void WithArrayNotation_MapStaticDelegateMethodWithVariousOverloadsUsingObjectNotation_RegistersCorrectOverload()
      {
         var json = @$"
[{{
   'Name': 'AddDummyDelegateWithInt',
   'DummyDelegate': '{NameOf<DelegateMembers>()}::{nameof(DelegateMembers.TestDelegateOverload)}'
}},{{
   'Name': 'AddDummyDelegateWithString',
   'DummyDelegate': '{NameOf<DelegateMembers>()}::{nameof(DelegateMembers.TestDelegateOverload)}'
}},{{
   'Name': 'AddDummyDelegateWithIntAndString',
   'DummyDelegate': '{NameOf<DelegateMembers>()}::{nameof(DelegateMembers.TestDelegateOverload)}'
}}]";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(Action<int>), sd.ServiceType),
             sd => Assert.Equal(typeof(Action<string>), sd.ServiceType),
             sd => Assert.Equal(typeof(Func<string, int, bool>), sd.ServiceType));
      }

      [Fact]
      public void WithArrayNotation_MapNonStaticMethodToDelegate_ThrowsInvalidOperationException()
      {
         var json = @$"
[{{
   'Name': 'AddDummyDelegate',
   'DummyDelegate': ' {NameOf<DelegateMembers>()} :: {nameof(DelegateMembers.NonStaticTestDelegate)} '
}}]";

         Assert.Throws<InvalidOperationException>(() => ProcessJson(json));
      }

      [Fact]
      public void WithArrayNotation_MapGenericMethodUsingStringNotation_RegistersService()
      {
         var json = @$"[ 'DummyGeneric`{NameOf<DummyTestClass>()}' ]";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(DummyTestClass), sd.ServiceType));
      }

      [Fact]
      public void WithArrayNotation_MapGenericMethodUsingObjectNotation_RegistersService()
      {
         var json = @$"
[{{
   'Name': 'DummyGeneric`{NameOf<DummyTestClass>()}'
}}]";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(DummyTestClass), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapGenericMethodUsingWithTrueValue_RegistersService()
      {
         var json = @$"
{{
   'DummyGeneric<{NameOf<DummyTestClass>()}>' : true
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(DummyTestClass), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapGenericMethodUsingWithFalseValue_SkipsRegistration()
      {
         var json = @$"
{{
   'DummyGeneric<{NameOf<DummyTestClass>()}>' : false
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Empty(serviceCollection);
      }

      [Fact]
      public void WithObjectNotation_MapTypeDictionaryUsingObjectNotation_RegistersService()
      {
         var json = @$"
{{
   'DummyTypeMap': {{
      'Mappings': {{
            '{NameOf<DummyTestClass>()}': '{NameOf<DummyTestClass>()}',
            '{NameOf<DummyParameter>()}': '{NameOf<DummyParameter>()}'
      }}
   }}
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(Dictionary<Type, Type>), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapTypeDictionaryDirectly_RegistersService()
      {
         var json = @$"
{{
   'DummyTypeMap': {{
      '{NameOf<DummyTestClass>()}': '{NameOf<DummyTestClass>()}',
      '{NameOf<DummyParameter>()}': '{NameOf<DummyParameter>()}'
   }}
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(Dictionary<Type, Type>), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapStringDictionaryDirectly_RegistersService()
      {
         var json = @$"
{{
   'DummyStringMap': {{
      'Hello': 'konnichiwa',
      'Good morning': 'ohayou',
      'Goodbye': 'dozvidanya'
   }}
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(Dictionary<string, string>), sd.ServiceType));
      }

      [Theory]
      [InlineData("Hello", false)]
      [InlineData("Test", true)]
      [InlineData("1246", true)]
      public void WithObjectNotation_MapStringDictionaryWithOverload_ChoosesDictionaryOverloadBasedOnPropertyName(string firstPropertyName, bool usesDictionaryOverload)
      {
         var json = @$"
{{
   'DummyStringMapInvalid': {{
      '{firstPropertyName}': 'konnichiwa',
      'Good morning': 'ohayou',
      'Goodbye': 'dozvidanya'
   }}
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(usesDictionaryOverload ? typeof(Dictionary<string, string>) : typeof(SimpleValue<string>), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapTypeListUsingObjectNotation_RegistersService()
      {
         var json = @$"
{{
   'DummyTypeList': {{
      'Types': [
            '{NameOf<DummyTestClass>()}',
            '{NameOf<DummyParameter>()}'
      ]
   }}
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(List<Type>), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapTypeListDirectly_RegistersService()
      {
         var json = @$"
{{
   'DummyTypeList': [
      '{NameOf<DummyTestClass>()}',
      '{NameOf<DummyParameter>()}'
   ]
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(List<Type>), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapTypeArrayDirectly_RegistersService()
      {
         var json = @$"
{{
   'DummyTypeArray': [
      '{NameOf<DummyTestClass>()}',
      '{NameOf<DummyParameter>()}'
   ]
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(Type[]), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapStringArrayUsingArrayNotationWithSingleStringOverloadUsingArrayWithMultipleElements_RegistersWithArrayOverload()
      {
         var json = @$"
{{
   'DummyString': [
      'hello',
      'world'
   ]
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(string[]), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapStringArrayUsingArrayNotationWithSingleStringOverloadUsingArrayWithSingleElement_RegistersWithArrayOverload()
      {
         var json = @$"
{{
   'DummyString': [
      'hello'
   ]
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(string[]), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapUsingStringDirectlyWithStringArrayOverload_RegistersWithSingleStringOverload()
      {
         var json = @$"
{{
   'DummyString': 'hello'
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(SimpleValue<string>), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapTypeArrayUsingObjectNotation_RegistersService()
      {
         var json = @$"
{{
   'DummyTypeArray': {{
      'Types': [
            '{NameOf<DummyTestClass>()}',
            '{NameOf<DummyParameter>()}'
      ]
   }}
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(Type[]), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapStringListDirectly_RegistersService()
      {
         var json = @$"
{{
   'DummyStringList': [
      'hello',
      'world'
   ]
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(List<string>), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapToExtensionMethodWithSingleStringParameterUsingStringValue_RegistersService()
      {
         var json = @$"
{{
   'SimpleString': 'hello'
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(SimpleValue<string>), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapToExtensionMethodWithSingleIntegerParameterUsingNumberValue_RegistersService()
      {
         var json = @$"
{{
   'SimpleInt32': 42
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(SimpleValue<int>), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapToOverloadedExtensionMethodUsingNamedStringParameter_RegistersCorrectOverload()
      {
         var json = @$"
{{
   'SimpleValue': {{ 'StringValue': 'hello' }}
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(SimpleValue<string>), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapToOverloadedExtensionMethodUsingNamedIntParameter_RegistersCorrectOverload()
      {
         var json = @$"
{{
   'SimpleValue': {{ 'IntValue': 42 }}
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(SimpleValue<int>), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapToOverloadedExtensionMethodWithNoParameters_RegistersCorrectOverload()
      {
         var json = @$"
{{
   'SimpleValue': true
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(SimpleValue<object>), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapToExtensionMethodWithSingleDelegateParameterDirectly_RegistersService()
      {
         var json = @$"
{{
   'SimpleDelegate': '{NameOf<ConfigurationBuilderTestsBase>()}::{nameof(DummyDelegateProperty)}'
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(SimpleValue<Delegate>), sd.ServiceType));
      }

      [Fact]
      public void WithObjectNotation_MapToExtensionMethodWithSingleTypeParameterDirectly_RegistersService()
      {
         var json = @$"
{{
   'SimpleType': '{NameOf<ConfigurationBuilderTestsBase>()}'
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Equal(typeof(SimpleValue<Type>), sd.ServiceType));
      }

      [Fact]
      public void WithArrayNotation_MapToExtensionMethodAcceptingComplexObject_InstantiatesObjectAndBindsValues()
      {
         var json = @$"
[{{
   'Name': 'AddComplexObject',
   'Parameter': {{
      'Name': 'hello',
      'Value': {{
            'Time' : '13:00:10'
      }}
   }}
}}]";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd =>
             {
                Assert.Equal(typeof(ComplexObject), sd.ServiceType);
                var config = Assert.IsType<ComplexObject>(sd.ImplementationInstance);
                Assert.Equal("hello", config.Name);
                Assert.NotNull(config.Value);
                Assert.Equal(new TimeSpan(13, 0, 10), config.Value.Time);
             });
      }

      [Fact]
      public void WithObjectNotation_MapToExtensionMethodAcceptingComplexObjectViaParameterName_InstantiatesObjectAndBindsValues()
      {
         var json = @$"
{{
   'ComplexObject': {{
      'Parameter': {{
            'Name': 'hello',
            'Value': {{
               'Time' : '13:00:10'
            }}
      }}
   }}
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd =>
             {
                Assert.Equal(typeof(ComplexObject), sd.ServiceType);
                var config = Assert.IsType<ComplexObject>(sd.ImplementationInstance);
                Assert.Equal("hello", config.Name);
                Assert.NotNull(config.Value);
                Assert.Equal(new TimeSpan(13, 0, 10), config.Value.Time);
             });
      }

      [Fact]
      public void WithObjectNotation_MapToExtensionMethodAcceptingComplexObjectDirectly_InstantiatesObjectAndBindsValues()
      {
         var json = @$"
{{
   'ComplexObject': {{
      'Name': 'hello',
      'Value': {{
            'Time' : '13:00:10'
      }}
   }}
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd =>
             {
                Assert.Equal(typeof(ComplexObject), sd.ServiceType);
                var config = Assert.IsType<ComplexObject>(sd.ImplementationInstance);
                Assert.Equal("hello", config.Name);
                Assert.NotNull(config.Value);
                Assert.Equal(new TimeSpan(13, 0, 10), config.Value.Time);
             });
      }

      [Fact]
      public void WithArrayNotation_MapToExtensionMethodAcceptingComplexObjectListViaParameterName_InstantiatesObjectAndBindsValues()
      {
         var json = @$"
[{{
   'Name': 'AddComplexObjects',
   'Parameters': [{{
      'Name': 'hello',
      'Value': {{
            'Time' : '13:00:10'
      }}
   }},{{
      'Name': 'hi',
      'Value': {{
            'Time' : '02:00:33'
      }}
   }}]
}}]";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd =>
             {
                Assert.Equal(typeof(List<ComplexObject>), sd.ServiceType);
                var configs = Assert.IsType<List<ComplexObject>>(sd.ImplementationInstance);

                Assert.Collection(
                       configs,
                       config =>
                       {
                          Assert.Equal("hello", config.Name);
                          Assert.NotNull(config.Value);
                          Assert.Equal(new TimeSpan(13, 0, 10), config.Value.Time);
                       },
                       config =>
                       {
                          Assert.Equal("hi", config.Name);
                          Assert.NotNull(config.Value);
                          Assert.Equal(new TimeSpan(02, 0, 33), config.Value.Time);
                       });
             });
      }

      [Fact]
      public void WithObjectNotation_MapToExtensionMethodAcceptingComplexObjectListDirectly_InstantiatesObjectAndBindsValues()
      {
         var json = @$"
{{
   'ComplexObjects': [{{
      'Name': 'hello',
      'Value': {{
            'Time' : '13:00:10'
      }}
   }},{{
      'Name': 'hi',
      'Value': {{
            'Time' : '02:00:33'
      }}
   }}]
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd =>
             {
                Assert.Equal(typeof(List<ComplexObject>), sd.ServiceType);
                var configs = Assert.IsType<List<ComplexObject>>(sd.ImplementationInstance);

                Assert.Collection(
                       configs,
                       config =>
                       {
                          Assert.Equal("hello", config.Name);
                          Assert.NotNull(config.Value);
                          Assert.Equal(new TimeSpan(13, 0, 10), config.Value.Time);
                       },
                       config =>
                       {
                          Assert.Equal("hi", config.Name);
                          Assert.NotNull(config.Value);
                          Assert.Equal(new TimeSpan(02, 0, 33), config.Value.Time);
                       });
             });
      }

      [Fact]
      public void WithObjectNotation_MapToGenericExtensionMethodUsingDynamicClass_CreatesDynamicClass()
      {
         var json = @$"
{{
   'Dynamic<!Abcd>': {{
      'Types': [
            '{NameOf<DummyTestClass>()}'
      ]
   }}
}}";
         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Collection(
                 Assert.IsType<List<Type>>(sd.ImplementationInstance),
                 t => Assert.Equal(typeof(DummyTestClass), t),
                 t => Assert.Equal("Abcd", t.Name)));
      }

      [Fact]
      public void WithObjectNotation_MapToGenericExtensionMethodUsingDynamicDerivedClass_CreatesDerivedClass()
      {
         var json = @$"
{{
   'DynamicDerived<!Abcde@{NameOf<ParentClass>()}>': {{
      'Types': [
            '{NameOf<DummyTestClass>()}'
      ]
   }}
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd => Assert.Collection(
                 Assert.IsType<List<Type>>(sd.ImplementationInstance),
                 t => Assert.Equal(typeof(DummyTestClass), t),
                 t => Assert.True(typeof(ParentClass).IsAssignableFrom(t))));
      }

      [Fact]
      public void WithObjectNotation_MapToExtensionMethodAcceptingConfigurationSection_ProvidesConfigurationSectionForCurrentContext()
      {
         var json = @$"
{{
   'Configuration': {{
      'Types': [
            '{NameOf<DummyTestClass>()}'
      ]
   }}
}}";

         var serviceCollection = ProcessJson(json);

         Assert.Collection(
             serviceCollection,
             sd =>
             {
                var section = Assert.IsAssignableFrom<IConfigurationSection>(sd.ImplementationInstance);
                Assert.True(section.GetSection("Types").Exists());
             });
      }

      [Fact]
      public void WithObjectNotation_MapToExtensionMethodAcceptingConfigurationActionDelegate_GeneratesConfigurationActionBasedOnObject()
      {
         var json = @$"
{{
   'ConfigurationAction': {{
      'Name': 'hello',
      'Value': {{
         'Time' : '13:00:10',
         'Location': 'http://www.google.com',
         'ContextType': '{NameOf<SimpleObject>()}',
         'OnError': '{NameOf<ConfigurationBuilderTestsBase>()}::{nameof(DummyDelegateField)}'
      }}
   }}
}}";

         var sp = BuildFromJson(json);
         var option = sp.GetService<IOptions<ComplexObject>>();

         Assert.NotNull(option);
         Assert.Equal("hello", option.Value.Name);
         Assert.Equal(new TimeSpan(13, 0, 10), option.Value.Value.Time);
         Assert.Equal("http://www.google.com/", option.Value.Value.Location.ToString());
         Assert.Equal(typeof(SimpleObject), option.Value.Value.ContextType);
         Assert.Equal(DummyDelegateField, option.Value.Value.OnError);
      }

      [Fact]
      public void WithObjectNotation_MapToExtensionMethodAcceptingConfigurationActionDelegateConfigureChildWithNullValues_GeneratedChildObjectHasDefaultMembers()
      {
         var json = @$"
{{
   'ConfigurationAction': {{
      'Name': 'hello',
      'Value': {{
         'Child': null,
         'Time': null,
         'Location': null,
         'ContextType': null,
         'OnError': null,
      }}
   }}
}}";

         var sp = BuildFromJson(json);
         var option = sp.GetService<IOptions<ComplexObject>>();

         Assert.NotNull(option);
         Assert.Equal("hello", option.Value.Name);
         var childValue = option.Value.Value;
         Assert.Empty(childValue.Child);
         Assert.Null(childValue.Time);
         Assert.Null(childValue.Location);
         Assert.Null(childValue.ContextType);
         Assert.Null(childValue.OnError);
      }

      [Fact]
      public void WithObjectNotation_MapToExtensionMethodAcceptingConfigurationActionDelegateConfigureChildWithEmptyStringValues_GeneratedChildObjectHasDefaultMembers()
      {
         var json = @$"
{{
   'ConfigurationAction': {{
      'Name': 'hello',
      'Value': {{
         'Child': '',
         'Time': '',
         'Location': '',
         'ContextType': '',
         'OnError': '',
      }}
   }}
}}";

         var sp = BuildFromJson(json);
         var option = sp.GetService<IOptions<ComplexObject>>();

         Assert.NotNull(option);
         Assert.Equal("hello", option.Value.Name);
         var childValue = option.Value.Value;
         Assert.Empty(childValue.Child);
         Assert.Null(childValue.Time);
         Assert.Null(childValue.Location);
         Assert.Null(childValue.ContextType);
         Assert.Null(childValue.OnError);
      }

      [Fact]
      public void WithObjectNotation_MapToExtensionMethodAcceptingConfigurationActionDelegateWithTrueString_GeneratesEmptyConfigurationAction()
      {
         var json = @$"
{{
   'ConfigurationAction': true
}}";

         var sp = BuildFromJson(json);
         var option = sp.GetService<IOptions<ComplexObject>>();

         Assert.NotNull(option.Value);
      }

      [Fact]
      public void WithObjectNotation_MapToExtensionMethodAcceptingConfigurationActionDelegate_CanCallExtensionMethodsForConfigurationObjectWithObjectNotation()
      {
         var json = @$"
{{
   'ConfigurationAction': {{
      'ConfigureName': {{
            'Name': 'hello'
      }},
      'ConfigureValue': {{
            'Time' : '13:00:10'
      }}
   }}
}}";

         var sp = BuildFromJson(json);
         var option = sp.GetService<IOptions<ComplexObject>>();

         Assert.NotNull(option);
         Assert.Equal("hello", option.Value.Name);
         Assert.Equal(new TimeSpan(13, 0, 10), option.Value.Value.Time);
      }

      [Fact]
      public void WithObjectNotation_MapToExtensionMethodAcceptingConfigurationActionDelegate_CanCallExtensionMethodsWithSingleStringParameterForConfigurationObjectWithString()
      {
         var json = @$"
{{
   'ConfigurationAction': {{
      'ConfigureName': 'hello'
   }}
}}";

         var sp = BuildFromJson(json);
         var option = sp.GetService<IOptions<ComplexObject>>();

         Assert.NotNull(option);
         Assert.Equal("hello", option.Value.Name);
      }

      [Fact]
      public void ConfigurationActionWithMethods()
      {
         var json = @$"
{{
   'ConfigurationAction': {{
      'SetName': {{
            'Name': 'hello'
      }}
   }}
}}";

         var sp = BuildFromJson(json);
         var option = sp.GetService<IOptions<ComplexObject>>();

         Assert.NotNull(option);
         Assert.Equal("hello", option.Value.Name);
      }

      [Fact]
      public void ConfigurationActionWithInterfaceExtensionMethods()
      {
         var json = @$"
{{
   'ConfigurationAction': {{
      'ConfigureByInterfaceValue': {{
            'Time' : '13:00:10'
      }}
   }}
}}";

         var sp = BuildFromJson(json);
         var option = sp.GetService<IOptions<ComplexObject>>();

         Assert.NotNull(option);
         Assert.Null(option.Value.Value.Child);
         Assert.Equal(new TimeSpan(13, 0, 10), option.Value.Value.Time);
      }

      [Fact]
      public void ConfigurationActionWithExtensionForInterfaceMethods()
      {
         var json = @$"
{{
   'ConfigurationAction': {{
      'ConfigureByInterfaceName': {{
            'Name': 'hello'
      }},
      'ConfigureByInterfaceValue': {{
            'Time' : '13:00:10'
      }}
   }}
}}";

         var sp = BuildFromJson(json);
         var option = sp.GetService<IOptions<ComplexObject>>();

         Assert.NotNull(option);
         Assert.Equal("hello", option.Value.Name);
         Assert.Equal(new TimeSpan(13, 0, 10), option.Value.Value.Time);
      }

      [Fact]
      public void ConfigurationActionWithGenericExtensionForInterfaceMethods()
      {
         var json = @$"
{{
   'ConfigurationAction': {{
      'ConfigureByInterfaceName<!hello>': true,
      'ConfigureByInterfaceValue': {{
          'Time' : '13:00:10'
      }}
   }}
}}";

         var sp = BuildFromJson(json);
         var option = sp.GetService<IOptions<ComplexObject>>();

         Assert.NotNull(option);
         Assert.Equal("hello", option.Value.Name);
         Assert.Equal(new TimeSpan(13, 0, 10), option.Value.Value.Time);
      }

      [Fact]
      public void WithObjectNotation_CallMethodOnConfigurationObject_ExecutesMethod()
      {
         var json = @$"
{{
   'ConfigurationAction': {{
      'Value': {{
         'Child': 'helloworld',
         'Time': '00:22:00',
      }},
      'Reset': true
   }}
}}";

         var sp = BuildFromJson(json);
         var option = sp.GetService<IOptions<ComplexObject>>();

         Assert.NotNull(option);
         Assert.Null(option.Value.Name);
         Assert.Null(option.Value.Value.Time);
      }

      [Fact]
      public void WithObjectNotation_CallMethodOnConfigurationChildObject_ExecutesMethod()
      {
         var json = @$"
{{
   'ConfigurationAction': {{
      'Value': {{
         'Child': 'helloworld',
         'Time': '00:22:00',
         'Reset': true
      }}
   }}
}}";

         var sp = BuildFromJson(json);
         var option = sp.GetService<IOptions<ComplexObject>>();

         Assert.NotNull(option);
         Assert.Null(option.Value.Name);
         Assert.Null(option.Value.Value.Child);
      }

      [Fact]
      public void WithObjectNotation_CallExtensionMethodOnConfigurationChildObject_ExecutesMethod()
      {
         var json = @$"
{{
   'ConfigurationAction': {{
      'Value': {{
         'Child': 'helloworld',
         'Location': 'www.google.com',
         'Time': '00:22:00',
         'Reset2': true
      }}
   }}
}}";

         var sp = BuildFromJson(json);
         var option = sp.GetService<IOptions<ComplexObject>>();

         Assert.NotNull(option);
         Assert.Null(option.Value.Name);
         Assert.Null(option.Value.Value.Time);
      }

      [Fact]
      public void WithObjectNotation_CallExtensionMethodOnConfigurationObject_ExecutesInAlphabeticalOrderOfConfiguration()
      {
         // IConfiguration sorts the keys when calling GetChildren()
         var json = @$"
{{
   'ConfigurationAction': {{
      'Name': 'helloworld',
      'Append<@x>': {{ 'value': '1' }},
      'Append<@a>': {{ 'value': '2' }},
      'Append<@b>': {{ 'value': '3' }},
      'Append<@y>': {{ 'value': '4' }},
   }}
}}";

         var sp = BuildFromJson(json);
         var option = sp.GetService<IOptions<ComplexObject>>();
         Assert.Equal("helloworld2314", option.Value.Name);
      }

      private IServiceProvider BuildFromJson(string json)
      {
         var serviceCollection = ProcessJson(json);
         return serviceCollection.BuildServiceProvider();
      }

      protected abstract IServiceCollection ProcessJson(string json);


      public class DummyTestClass
      {
      }

      public class DelegateMembers
      {
         public void NonStaticTestDelegate()
         {
         }

         public static void TestDelegate()
         {
         }

         public static void TestDelegateOverload(string value)
         {
         }

         public static void TestDelegateOverload(int value)
         {
         }

         public static bool TestDelegateOverload(string svalue = null, int ivalue = 0)
         {
            return true;
         }
      }
   }
}
