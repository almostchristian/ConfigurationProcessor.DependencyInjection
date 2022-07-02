// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

using ConfigurationProcessor.DependencyInjection.UnitTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        public void ArrayMixRegistrations()
        {
            var json = @$"
{{
    'Services': [
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
    ]
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

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
        public void DummyDelegate()
        {
            var json = @$"
{{
    'Services': [ {{
        'Name': 'AddDummyDelegate',
        'DummyDelegate': '{NameOf<DelegateMembers>()}::{nameof(DelegateMembers.TestDelegate)}'
    }}
]
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(DummyDelegate), sd.ServiceType));
        }

        [Fact]
        public void DummyDelegateViaProperty()
        {
            var json = @$"
{{
    'Services': [ {{
        'Name': 'AddDummyDelegate',
        'DummyDelegate': '{NameOf<ConfigurationBuilderTestsBase>()}::{nameof(DummyDelegateProperty)}'
    }}
]
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(DummyDelegate), sd.ServiceType));
        }

        [Fact]
        public void DummyDelegateViaField()
        {
            var json = @$"
{{
    'Services': [ {{
        'Name': 'AddDummyDelegate',
        'DummyDelegate': '{NameOf<ConfigurationBuilderTestsBase>()}::{nameof(DummyDelegateField)}'
    }}
]
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(DummyDelegate), sd.ServiceType));
        }

        [Fact]
        public void DummyDelegateOverloadResolution()
        {
            var json = @$"
{{
    'Services': [ {{
        'Name': 'AddDummyDelegateWithInt',
        'DummyDelegate': '{NameOf<DelegateMembers>()}::{nameof(DelegateMembers.TestDelegateOverload)}'
    }},{{
        'Name': 'AddDummyDelegateWithString',
        'DummyDelegate': '{NameOf<DelegateMembers>()}::{nameof(DelegateMembers.TestDelegateOverload)}'
    }},{{
        'Name': 'AddDummyDelegateWithIntAndString',
        'DummyDelegate': '{NameOf<DelegateMembers>()}::{nameof(DelegateMembers.TestDelegateOverload)}'
    }} ]
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(Action<int>), sd.ServiceType),
                sd => Assert.Equal(typeof(Action<string>), sd.ServiceType),
                sd => Assert.Equal(typeof(Func<string, int, bool>), sd.ServiceType));
        }

        [Fact]
        public void AddingWithDelegateThrowsInvalidOperationExceptionIfDelegateNotStatic()
        {
            var json = @$"
{{
    'Services': [ {{
        'Name': 'DummyDelegate',
        'DummyDelegate': '{NameOf<DelegateMembers>()}::{nameof(DelegateMembers.NonStaticTestDelegate)}'
    }}
]
}}";

            var serviceCollection = new TestServiceCollection();

            Assert.Throws<InvalidOperationException>(() => TestBuilder(json, serviceCollection));
        }

        [Fact]
        public void AddingGenericWithSimplifiedSyntax()
        {
            var json = @$"
{{
    'Services': [ 'DummyGeneric`{NameOf<DummyTestClass>()}' ]
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(DummyTestClass), sd.ServiceType));
        }

        [Fact]
        public void AddingGenericWithExpandedSyntax()
        {
            var json = @$"
{{
    'Services': [ {{
        'Name': 'DummyGeneric`{NameOf<DummyTestClass>()}'
    }} ]
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(DummyTestClass), sd.ServiceType));
        }

        [Fact]
        public void AddingGenericWithAlternateSyntax()
        {
            var json = @$"
{{
    'Services': {{
        'DummyGeneric`{NameOf<DummyTestClass>()}' : true
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(DummyTestClass), sd.ServiceType));
        }

        [Fact]
        public void AddingTypeMapWithAlternateSyntax()
        {
            var json = @$"
{{
    'Services': {{
        'DummyTypeMap': {{
            'Mappings': {{
                '{NameOf<DummyTestClass>()}': '{NameOf<DummyTestClass>()}',
                '{NameOf<DummyParameter>()}': '{NameOf<DummyParameter>()}'
            }}
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(Dictionary<Type, Type>), sd.ServiceType));
        }

        [Fact]
        public void AddingTypeMapWithoutParameterNameSyntax()
        {
            var json = @$"
{{
    'Services': {{
        'DummyTypeMap': {{
            '{NameOf<DummyTestClass>()}': '{NameOf<DummyTestClass>()}',
            '{NameOf<DummyParameter>()}': '{NameOf<DummyParameter>()}'
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(Dictionary<Type, Type>), sd.ServiceType));
        }

        [Fact]
        public void AddingTypeListWithAlternateSyntax()
        {
            var json = @$"
{{
    'Services': {{
        'DummyTypeList': {{
            'Types': [
                '{NameOf<DummyTestClass>()}',
                '{NameOf<DummyParameter>()}'
            ]
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(List<Type>), sd.ServiceType));
        }

        [Fact]
        public void AddingTypeListWithoutParameterNameSyntax()
        {
            var json = @$"
{{
    'Services': {{
        'DummyTypeList': [
            '{NameOf<DummyTestClass>()}',
            '{NameOf<DummyParameter>()}'
        ]
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(List<Type>), sd.ServiceType));
        }

        [Fact]
        public void AddingStringListWithoutParameterNameSyntax()
        {
            var json = @$"
{{
    'Services': {{
        'DummyStringList': [
            'hello',
            'world'
        ]
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(List<string>), sd.ServiceType));
        }

        [Fact]
        public void AddingSimpleStringWithoutParameterNameSyntax()
        {
            var json = @$"
{{
    'Services': {{
        'SimpleString': 'hello'
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(SimpleValue<string>), sd.ServiceType));
        }

        [Fact]
        public void AddingSimpleIntWithoutParameterNameSyntax()
        {
            var json = @$"
{{
    'Services': {{
        'SimpleInt32': 42
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(SimpleValue<int>), sd.ServiceType));
        }

        [Fact]
        public void AddingSimpleDelegateWithoutParameterNameSyntax()
        {
            var json = @$"
{{
    'Services': {{
        'SimpleDelegate': '{NameOf<ConfigurationBuilderTestsBase>()}::{nameof(DummyDelegateProperty)}'
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(SimpleValue<Delegate>), sd.ServiceType));
        }

        [Fact]
        public void AddingSimpleTypeWithoutParameterNameSyntax()
        {
            var json = @$"
{{
    'Services': {{
        'SimpleType': '{NameOf<ConfigurationBuilderTestsBase>()}'
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(SimpleValue<Type>), sd.ServiceType));
        }

        [Fact]
        public void AddingTypeArrayWithAlternateSyntax()
        {
            var json = @$"
{{
    'Services': {{
        'DummyTypeArray': {{
            'Types': [
                '{NameOf<DummyTestClass>()}',
                '{NameOf<DummyParameter>()}'
            ]
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(Type[]), sd.ServiceType));
        }

        [Fact]
        public void AddingTypeArrayWithoutParameterNameSyntax()
        {
            var json = @$"
{{
    'Services': {{
        'DummyTypeArray': [
            '{NameOf<DummyTestClass>()}',
            '{NameOf<DummyParameter>()}'
        ]
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(Type[]), sd.ServiceType));
        }

        [Fact]
        public void AddingStringArrayWithoutParameterNameSyntax()
        {
            var json = @$"
{{
    'Services': {{
        'DummyStringArray': [
            'hello',
            'world'
        ]
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(string[]), sd.ServiceType));
        }

        [Fact]
        public void ComplexObject()
        {
            var json = @$"
{{
    'Services': [ {{
        'Name': 'AddComplexObject',
        'Parameter': {{
            'Name': 'hello',
            'Value': {{
                'Time' : '13:00:10'
            }}
        }}
    }}
]
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

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
        public void ComplexObjectAlternateSyntax()
        {
            var json = @$"
{{
    'Services': {{
        'ComplexObject': {{
            'Parameter': {{
                'Name': 'hello',
                'Value': {{
                    'Time' : '13:00:10'
                }}
            }}
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

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
        public void ComplexObjectAlternateSyntaxSimplified()
        {
            var json = @$"
{{
    'Services': {{
        'ComplexObject': {{
            'Name': 'hello',
            'Value': {{
                'Time' : '13:00:10'
            }}
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

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
        public void ComplexObjects()
        {
            var json = @$"
{{
    'Services': [{{
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
    }}
]
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

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
        public void DynamicClass()
        {
            var json = @$"
{{
    'Services': {{
        'Dynamic<!Abcd>': {{
            'Types': [
                '{NameOf<DummyTestClass>()}'
            ]
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Collection(
                    Assert.IsType<List<Type>>(sd.ImplementationInstance),
                    t => Assert.Equal(typeof(DummyTestClass), t),
                    t => Assert.Equal("Abcd", t.Name)));
        }

        [Fact]
        public void DynamicDerivedClass()
        {
            var json = @$"
{{
    'Services': {{
        'DynamicDerived<!Abcde@{NameOf<ParentClass>()}>': {{
            'Types': [
                '{NameOf<DummyTestClass>()}'
            ]
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.Collection(
                    Assert.IsType<List<Type>>(sd.ImplementationInstance),
                    t => Assert.Equal(typeof(DummyTestClass), t),
                    t => Assert.True(typeof(ParentClass).IsAssignableFrom(t))));
        }

        [Fact]
        public void Configuration()
        {
            var json = @$"
{{
    'Services': {{
        'Configuration': {{
            'Types': [
                '{NameOf<DummyTestClass>()}'
            ]
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            Assert.Collection(
                serviceCollection,
                sd => Assert.IsAssignableFrom<IConfigurationSection>(sd.ImplementationInstance));
        }

        [Fact]
        public void ConfigurationActionWithProperties()
        {
            var json = @$"
{{
    'Services': {{
        'ConfigurationAction': {{
            'Name': 'hello',
            'Value': {{
                'Time' : '13:00:10'
            }}
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            var sp = serviceCollection.BuildServiceProvider();
            var option = sp.GetService<IOptions<ComplexObject>>();

            Assert.NotNull(option);
            Assert.Equal("hello", option.Value.Name);
            Assert.Equal(new TimeSpan(13, 0, 10), option.Value.Value.Time);
        }

        [Fact]
        public void ConfigurationActionEmptyConfiguration()
        {
            var json = @$"
{{
    'Services': {{
        'ConfigurationAction': true
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            var sp = serviceCollection.BuildServiceProvider();
            var option = sp.GetService<IOptions<ComplexObject>>();

            Assert.NotNull(option.Value);
        }

        [Fact]
        public void ConfigurationActionWithExtensionMethods()
        {
            var json = @$"
{{
    'Services': {{
        'ConfigurationAction': {{
            'ConfigureName': {{
                'Name': 'hello'
            }},
            'ConfigureValue': {{
                'Time' : '13:00:10'
            }}
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            var sp = serviceCollection.BuildServiceProvider();
            var option = sp.GetService<IOptions<ComplexObject>>();

            Assert.NotNull(option);
            Assert.Equal("hello", option.Value.Name);
            Assert.Equal(new TimeSpan(13, 0, 10), option.Value.Value.Time);
        }

        [Fact]
        public void ConfigurationActionWithExtensionMethodsWithoutPropertyName()
        {
            var json = @$"
{{
    'Services': {{
        'ConfigurationAction': {{
            'ConfigureName': 'hello'
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            var sp = serviceCollection.BuildServiceProvider();
            var option = sp.GetService<IOptions<ComplexObject>>();

            Assert.NotNull(option);
            Assert.Equal("hello", option.Value.Name);
        }

        [Fact]
        public void ConfigurationActionWithMethods()
        {
            var json = @$"
{{
    'Services': {{
        'ConfigurationAction': {{
            'SetName': {{
                'Name': 'hello'
            }}
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            var sp = serviceCollection.BuildServiceProvider();
            var option = sp.GetService<IOptions<ComplexObject>>();

            Assert.NotNull(option);
            Assert.Equal("hello", option.Value.Name);
        }

        [Fact]
        public void ConfigurationActionWithInterfaceExtensionMethods()
        {
            var json = @$"
{{
    'Services': {{
        'ConfigurationAction': {{
            'ConfigureByInterfaceValue': {{
                'Time' : '13:00:10'
            }}
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            var sp = serviceCollection.BuildServiceProvider();
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
    'Services': {{
        'ConfigurationAction': {{
            'ConfigureByInterfaceName': {{
                'Name': 'hello'
            }},
            'ConfigureByInterfaceValue': {{
                'Time' : '13:00:10'
            }}
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            var sp = serviceCollection.BuildServiceProvider();
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
    'Services': {{
        'ConfigurationAction': {{
            'ConfigureByInterfaceName<!hello>': true,
            'ConfigureByInterfaceValue': {{
                'Time' : '13:00:10'
            }}
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            var sp = serviceCollection.BuildServiceProvider();
            var option = sp.GetService<IOptions<ComplexObject>>();

            Assert.NotNull(option);
            Assert.Equal("hello", option.Value.Name);
            Assert.Equal(new TimeSpan(13, 0, 10), option.Value.Value.Time);
        }

        [Fact]
        public void ConfigurationActionWithUpdateChildObject()
        {
            var json = @$"
{{
    'Services': {{
        'ConfigurationAction': {{
            'Value': {{
                'Child': 'helloworld',
                'Time': '00:22:00'
            }},
            'Reset': true
        }}
    }}
}}";

            var serviceCollection = new TestServiceCollection();
            TestBuilder(json, serviceCollection);

            var sp = serviceCollection.BuildServiceProvider();
            var option = sp.GetService<IOptions<ComplexObject>>();

            Assert.NotNull(option);
            Assert.Null(option.Value.Name);
            Assert.Equal(TimeSpan.Zero, option.Value.Value.Time);
        }

        protected abstract void TestBuilder(string json, TestServiceCollection serviceCollection, string fullJsonFormat = null);

        protected static IConfiguration CreateFromJson(string jsonString, string fullJsonFormat = "{{ 'Services': {0}}}")
        {
            var fullJson = string.Format(fullJsonFormat, jsonString);
            return new ConfigurationBuilder().AddJsonString(fullJson).Build();
        }


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
