// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

using ConfigurationProcessor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace TestDummies
{
   public delegate void DummyDelegate();

   [ExcludeFromCodeCoverage]
   public static class DummyServiceCollectionExtensions
   {
      public static IServiceCollection AddDummyDelegate(this IServiceCollection services, DummyDelegate dummyDelegate)
      {
         services.AddSingleton(dummyDelegate);
         return services;
      }

      public static IServiceCollection AddDummyDelegateWithInt(this IServiceCollection services, Action<int> dummyDelegate)
      {
         services.AddSingleton(dummyDelegate);
         return services;
      }

      public static IServiceCollection AddDummyDelegateWithString(this IServiceCollection services, Action<string> dummyDelegate)
      {
         services.AddSingleton(dummyDelegate);
         return services;
      }

      public static IServiceCollection AddDummyDelegateWithIntAndString(this IServiceCollection services, Func<string, int, bool> dummyDelegate)
      {
         services.AddSingleton(dummyDelegate);
         return services;
      }

      public static IServiceCollection AddDynamic<T>(this IServiceCollection services, List<Type> types)
          where T : class
      {
         types.Add(typeof(T));
         services.AddSingleton(types);
         return services;
      }

      public static IServiceCollection AddDynamicDerived<T>(this IServiceCollection services, List<Type> types)
          where T : ParentClass
      {
         types.Add(typeof(T));
         services.AddSingleton(types);
         return services;
      }

      public static IServiceCollection AddSimpleString(this IServiceCollection services, string value)
      {
         services.AddSingleton(new SimpleValue<string>(value));
         return services;
      }

      public static IServiceCollection AddSimpleInt32(this IServiceCollection services, int value, bool rev = false)
      {
         services.AddSingleton(new SimpleValue<int>(value));
         return services;
      }

      public static IServiceCollection AddSimpleValue(this IServiceCollection services, string stringValue)
      {
         services.AddSingleton(new SimpleValue<string>(stringValue));
         return services;
      }

      public static IServiceCollection AddSimpleValue(this IServiceCollection services, int intValue)
      {
         services.AddSingleton(new SimpleValue<int>(intValue));
         return services;
      }

      public static IServiceCollection AddSimpleValue(this IServiceCollection services)
      {
         services.AddSingleton(new SimpleValue<object>(new object()));
         return services;
      }

      public static IServiceCollection AddSimpleDelegate(this IServiceCollection services, Delegate value)
      {
         services.AddSingleton(new SimpleValue<Delegate>(value));
         return services;
      }

      public static IServiceCollection AddSimpleDelegate(this IServiceCollection services)
      {
         services.AddSingleton(new SimpleValue<object>(new object()));
         return services;
      }
      public static IServiceCollection AddSimpleType(this IServiceCollection services)
      {
         services.AddSingleton(new SimpleValue<object>(new object()));
         return services;
      }

      public static IServiceCollection AddSimpleType(this IServiceCollection services, Type value)
      {
         services.AddSingleton(new SimpleValue<Type>(value));
         return services;
      }

      public static IServiceCollection AddDummyGenericHandler<T>(this IServiceCollection services)
          where T : class
      {
         services.AddSingleton<T>();
         return services;
      }

      public static IServiceCollection AddDummyTypeMap(this IServiceCollection services, Dictionary<Type, Type> mappings)
      {
         services.AddSingleton(mappings);
         return services;
      }

      public static IServiceCollection AddDummyTypeActionMap(this IServiceCollection services, Dictionary<Type, Action<SimpleObject>> configurations)
      {
         services.AddSingleton(configurations);
         return services;
      }

      public static SimpleObject AppendString(this SimpleObject value, string toAppend)
      {
         value.PropertyB += toAppend;
         return value;
      }

      public static IServiceCollection AddDummyStringMap(this IServiceCollection services, Dictionary<string, string> mappings)
      {
         services.AddSingleton(mappings);
         return services;
      }

      public static IServiceCollection AddDummyStringMapInvalid(this IServiceCollection services, Dictionary<string, string> mappings)
      {
         services.AddSingleton(mappings);
         return services;
      }

      public static IServiceCollection AddDummyStringMapInvalid(this IServiceCollection services, string hello)
      {
         services.AddSingleton(new SimpleValue<string>(hello));
         return services;
      }

      public static IServiceCollection AddDummyTypeList(this IServiceCollection services, List<Type> types)
      {
         services.AddSingleton(types);
         return services;
      }

      public static IServiceCollection AddDummyStringList(this IServiceCollection services, List<string> types)
      {
         services.AddSingleton(types);
         return services;
      }

      public static IServiceCollection AddDummyTypeArray(this IServiceCollection services, Type[] types)
      {
         services.AddSingleton(types);
         return services;
      }

      public static IServiceCollection AddDummyArray(this IServiceCollection services, params int[] types)
      {
         services.AddSingleton(types);
         return services;
      }

      public static IServiceCollection AddDummyArray(this IServiceCollection services, params TimeSpan[] types)
      {
         services.AddSingleton(types);
         return services;
      }

      public static IServiceCollection AddDummyString(this IServiceCollection services, string value)
      {
         services.AddSingleton(new SimpleValue<string>(value));
         return services;
      }

      public static IServiceCollection AddDummyString(this IServiceCollection services, params string[] values)
      {
         services.AddSingleton(values);
         return services;
      }

      public static IServiceCollection AddComplexObject(this IServiceCollection services)
      {
         services.AddSingleton(new ComplexObject());
         return services;
      }

      public static IServiceCollection AddComplexObject(this IServiceCollection services, ComplexObject parameter)
      {
         services.AddSingleton(parameter);
         return services;
      }

      public static IServiceCollection AddComplexObjects(this IServiceCollection services, List<ComplexObject> parameters)
      {
         services.AddSingleton(parameters);
         return services;
      }

      public static IServiceCollection AddConfiguration(this IServiceCollection services, IConfigurationSection configuration)
      {
         services.AddSingleton(configuration);
         return services;
      }

      public static IServiceCollection AddConfigurationAction(this IServiceCollection services, Action<ComplexObject> configureOptions)
      {
         services.Configure(configureOptions);
         return services;
      }

      public static IServiceCollection AddConfigurationActionInterface(this IServiceCollection services, Action<IComplexObject> configureOptions)
      {
         var obj = new ComplexObject();
         configureOptions(obj);
         services.AddSingleton<IComplexObject>(obj);
         return services;
      }

      public static IServiceCollection AddConfigurationAction2(this IServiceCollection services, Action<SimpleObject> configureOptions)
      {
         services.Configure(configureOptions);
         return services;
      }

      public static void AddConfigureName(this ComplexObject configuration, string name, bool randomParam = true)
      {
         configuration.Name = name;
      }

      public static void AddConfigureValue(this ComplexObject configuration, Action<ComplexObject.ChildValue> configureValue)
      {
         configuration.Value ??= new ComplexObject.ChildValue();
         configureValue(configuration.Value);
      }

      public static void AddConfigureByInterfaceName<T>(this IComplexObject configuration)
          => configuration.AddConfigureByInterfaceName(typeof(T).Name);

      public static void AddConfigureByInterfaceName(this IComplexObject configuration, string name)
      {
         configuration.Name = name;
      }

      public static void AddConfigureByInterfaceValue(this IComplexObject configuration, Action<ComplexObject.ChildValue> configureValue)
      {
         configuration.Value ??= new ComplexObject.ChildValue();
         configureValue(configuration.Value);
      }

      public static void AddConfigureByInterfaceValueBaseInterface(this IComplexObject configuration, Action<IChildValue> configureValue)
      {
         configuration.Value ??= new ComplexObject.ChildValue();
         configureValue(configuration.Value);
      }

      public static void AddConfigureByInterfaceValue(this IComplexObject configuration, string childName = null, TimeSpan? timeSpan = null)
      {
         configuration.Value ??= new ComplexObject.ChildValue();
         configuration.Value.Child = childName;
         if (timeSpan != null)
         {
            configuration.Value.Time = timeSpan.Value;
         }
      }

      public static void Reset2(this ComplexObject.ChildValue childValue)
      {
         if (childValue != null)
         {
            childValue.Time = null;
         }
      }

      public static void Reset2(this IChildValue childValue)
      {
         if (childValue != null)
         {
            childValue.Time = null;
         }
      }

      public static void Append<T>(this ComplexObject obj, string value)
      {
         obj.Name += value;
      }

      public static IServiceCollection AddWithHelper(this IServiceCollection services, string text, IConfigurationProcessor helper)
      {
         services.AddConfigurationAction(c => helper.Invoke(c, "AddConfigureName", text));
         return services;
      }

      public static IServiceCollection AddWithHelper(this IServiceCollection services, Action<ComplexObject.ChildValue> configure, IConfigurationProcessor helper)
      {
         services.AddConfigurationAction(c => helper.Invoke(c, "AddConfigureValue", configure));
         return services;
      }

      public static IServiceCollection AddDbConnection(this IServiceCollection services, string connectionString)
      {
         return services.Configure<DbConnection>(conn => conn.ConnectionString = connectionString);
      }

      public static IServiceCollection ConfigureDbConnection(this IServiceCollection services, Action<DbConnection> configure)
      {
         return services.Configure(configure);
      }

      public static void ConnectionString(this ComplexObject obj, string value)
      {
         obj.Name = value;
      }

      public static IServiceCollection MultiParameterDelegateWithObject(this IServiceCollection services, Action<ComplexObject, DbConnection, object> configurator)
      {
         var complexObj = new ComplexObject();
         var dbConn = new DbConnection();
         configurator(complexObj, dbConn, new object());
         services.AddSingleton(Options.Create(complexObj));
         services.AddSingleton(Options.Create(dbConn));
         return services;
      }

      public static IServiceCollection MultiParameterDelegate2(this IServiceCollection services, Action<ComplexObject, DbConnection> configurator)
      {
         var complexObj = new ComplexObject();
         var dbConn = new DbConnection();
         configurator(complexObj, dbConn);
         services.AddSingleton(Options.Create(complexObj));
         services.AddSingleton(Options.Create(dbConn));
         return services;
      }

      public static IServiceCollection MultiParameterDelegate3(this IServiceCollection services, Action<ComplexObject, ComplexObject.ChildValue, DbConnection> configurator)
      {
         var complexObj = new ComplexObject { Value = new ComplexObject.ChildValue() };
         var dbConn = new DbConnection();
         configurator(complexObj, complexObj.Value, dbConn);
         services.AddSingleton(Options.Create(complexObj));
         services.AddSingleton(Options.Create(dbConn));
         return services;
      }

      public static IServiceCollection MultiParameterDelegate4(this IServiceCollection services, Action<ComplexObject, ComplexObject.ChildValue, DbConnection, IComplexObject> configurator)
      {
         var complexObj = new ComplexObject { Value = new ComplexObject.ChildValue() };
         var dbConn = new DbConnection();
         configurator(complexObj, complexObj.Value, dbConn, complexObj);
         services.AddSingleton(Options.Create(complexObj));
         services.AddSingleton(Options.Create(dbConn));
         return services;
      }

      public static IServiceCollection MultiParameterDelegate5(this IServiceCollection services, Action<ComplexObject, IChildValue, DbConnection, IComplexObject, IComplexObject> configurator)
      {
         var complexObj = new ComplexObject { Value = new ComplexObject.ChildValue() };
         var dbConn = new DbConnection();
         configurator(complexObj, complexObj.Value, dbConn, complexObj, complexObj);
         services.AddSingleton(Options.Create(complexObj));
         services.AddSingleton(Options.Create(dbConn));
         return services;
      }

      public static IServiceCollection MultiParameterDelegate6(this IServiceCollection services, Action<ComplexObject, ComplexObject.ChildValue, DbConnection, IComplexObject, IComplexObject, IComplexObject> configurator)
      {
         var complexObj = new ComplexObject { Value = new ComplexObject.ChildValue() };
         var dbConn = new DbConnection();
         configurator(complexObj, complexObj.Value, dbConn, complexObj, complexObj, complexObj);
         services.AddSingleton(Options.Create(complexObj));
         services.AddSingleton(Options.Create(dbConn));
         return services;
      }

      public static IServiceCollection MultiParameterDelegate7(this IServiceCollection services, Action<ComplexObject, IChildValue, DbConnection, IComplexObject, IComplexObject, IComplexObject, IComplexObject> configurator)
      {
         var complexObj = new ComplexObject { Value = new ComplexObject.ChildValue() };
         var dbConn = new DbConnection();
         configurator(complexObj, complexObj.Value, dbConn, complexObj, complexObj, complexObj, complexObj);
         services.AddSingleton(Options.Create(complexObj));
         services.AddSingleton(Options.Create(dbConn));
         return services;
      }

      public static IServiceCollection MultiParameterDelegate8(this IServiceCollection services, Action<ComplexObject, ComplexObject.ChildValue, DbConnection, ComplexObject, ComplexObject, ComplexObject, ComplexObject, ComplexObject> configurator)
      {
         var complexObj = new ComplexObject { Value = new ComplexObject.ChildValue() };
         var dbConn = new DbConnection();
         configurator(complexObj, complexObj.Value, dbConn, complexObj, complexObj, complexObj, complexObj, complexObj);
         services.AddSingleton(Options.Create(complexObj));
         services.AddSingleton(Options.Create(dbConn));
         return services;
      }

      public static void MultiConfigureComplex(ComplexObject Obj, DbConnection Conn, Action<ComplexObject> configureOptions)
      {
         configureOptions(Obj);
      }

      public static void MultiConfigureArray(ComplexObject Obj, DbConnection Conn, string[] names)
      {
         Obj.Name += names.ElementAtOrDefault(0);
         Conn.ConnectionString += names.ElementAtOrDefault(1);
      }

      public static void MultiConfigure(ComplexObject Obj, DbConnection Conn, string value)
      {
         Obj.Name += value;
         Conn.ConnectionString += value;
      }

      public static void MultiConfigure(ComplexObject Obj, ComplexObject.ChildValue Child, DbConnection Conn, string value)
      {
         Obj.Name += value;
         Conn.ConnectionString += value;
      }

      public static void MultiConfigure(ComplexObject Obj, IChildValue Child, DbConnection Conn, IComplexObject Obj1, string value)
      {
         Obj.Name += value;
         Conn.ConnectionString += value;
      }

      public static void MultiConfigure(ComplexObject Obj, IChildValue Child, DbConnection Conn, IComplexObject Obj1, IComplexObject Obj2, string value)
      {
         Obj.Name += value;
         Conn.ConnectionString += value;
      }

      public static void MultiConfigure(ComplexObject Obj, ComplexObject.ChildValue Child, DbConnection Conn, IComplexObject Obj1, IComplexObject Obj2, IComplexObject Obj3, string value)
      {
         Obj.Name += value;
         Conn.ConnectionString += value;
      }

      public static void MultiConfigure(ComplexObject Obj, IChildValue Child, DbConnection Conn, IComplexObject Obj1, IComplexObject Obj2, IComplexObject Obj3, IComplexObject Obj4, string value)
      {
         Obj.Name += value;
         Conn.ConnectionString += value;
      }

      public static void MultiConfigure(ComplexObject Obj, IChildValue Child, DbConnection Conn, ComplexObject Obj1, ComplexObject Obj2, ComplexObject Obj3, ComplexObject Obj4, ComplexObject Obj5, string value)
      {
         Obj.Name += value;
         Conn.ConnectionString += value;
      }
   }
}
