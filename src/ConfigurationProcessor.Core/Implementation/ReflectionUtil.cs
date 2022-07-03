// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace ConfigurationProcessor.Core.Implementation
{
   internal static class ReflectionUtil
   {
      private static readonly Lazy<ModuleBuilder> LazyModuleBuilder = new Lazy<ModuleBuilder>(() => InitializeModuleBuilder());
      private static readonly ConcurrentDictionary<(string Name, Type? ParentType), Type> DynamicTypes = new();

      private static ModuleBuilder InitializeModuleBuilder()
      {
         var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString()), AssemblyBuilderAccess.Run);
         return assemblyBuilder.DefineDynamicModule("DynamicClass");
      }

      /// <summary>
      /// Returns the MethodInfo used in the method expression. If the method is a generic type, the generic method definition is returned.
      /// </summary>
      /// <param name="methodCallExpression">The method call expression.</param>
      /// <typeparam name="T">The input parameter.</typeparam>
      /// <returns>The relected <see cref="MethodInfo"/> in the expression body.</returns>
      /// <exception cref="ArgumentException">Thrown when the expression body does not end with a method call.</exception>
      public static MethodInfo GetMethodInfo<T>(Expression<Action<T>> methodCallExpression)
          => GetMethodInfo((LambdaExpression)methodCallExpression);

      private static MethodInfo GetMethodInfo(LambdaExpression methodCallExpr)
      {
         var baseExpression = methodCallExpr.Body;

         if (baseExpression is UnaryExpression unExpr && unExpr.NodeType == ExpressionType.Convert)
         {
            baseExpression = unExpr.Operand;
         }

         if (!(baseExpression is MethodCallExpression methodCall))
         {
            throw new ArgumentException("Argument must be a call to a method", nameof(methodCallExpr));
         }

         var method = methodCall.Method;

         return method.IsGenericMethod ? method.GetGenericMethodDefinition() : method;
      }

      public static Type CreateType(string name)
          => CreateType(name, null);

      public static Type CreateType(string name, Type? parentType)
          => CreateType(name, parentType, Array.Empty<Expression<Func<Attribute>>>());

      public static Type CreateType(string name, Type? parentType, params Expression<Func<Attribute>>[] attributesExpressions)
      {
         if (name == null)
         {
            throw new ArgumentNullException(nameof(name));
         }

         return DynamicTypes.GetOrAdd((name, parentType), n =>
         {
            var moduleBuilder = LazyModuleBuilder.Value;

            TypeBuilder tb;
            if (n.ParentType != null && n.ParentType.IsGenericTypeDefinition)
            {
               tb = moduleBuilder.DefineType(n.Name, TypeAttributes.AutoClass | TypeAttributes.BeforeFieldInit);

               var derivedParentType = n.ParentType.MakeGenericType(tb);
               tb.SetParent(derivedParentType);

               // assumes protected constructors
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
               var declaredConstructor = n.ParentType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields

               foreach (var constructor in declaredConstructor)
               {
                  var parameters = constructor.GetParameters();
                  if (parameters.Length > 0 && parameters.Last().IsDefined(typeof(ParamArrayAttribute), false))
                  {
                     throw new InvalidOperationException("Variadic constructors are not supported");
                  }

                  var parameterTypes = parameters.Select(p => TransformParameterType(p.ParameterType)).ToArray();
                  var requiredCustomModifiers = parameters.Select(p => p.GetRequiredCustomModifiers()).ToArray();
                  var optionalCustomModifiers = parameters.Select(p => p.GetOptionalCustomModifiers()).ToArray();

                  var ctor = tb.DefineConstructor(MethodAttributes.Public, constructor.CallingConvention, parameterTypes, requiredCustomModifiers, optionalCustomModifiers);

                  var emitter = ctor.GetILGenerator();
                  emitter.Emit(OpCodes.Nop);

                  // Load `this` and call base constructor with arguments
                  emitter.Emit(OpCodes.Ldarg_0);
                  for (var i = 1; i <= parameters.Length; ++i)
                  {
                     emitter.Emit(OpCodes.Ldarg, i);
                  }

                  var derivedConstructor = TypeBuilder.GetConstructor(derivedParentType, constructor);
                  emitter.Emit(OpCodes.Call, derivedConstructor);

                  emitter.Emit(OpCodes.Ret);

                  Type TransformParameterType(Type parameter)
                  {
                     if (parameter.IsGenericType)
                     {
                        var genParams = parameter.GenericTypeArguments;
                        var rewrittenGenParams = new Type[genParams.Length];
                        for (int i = 0; i < genParams.Length; i++)
                        {
                           var genParam = genParams[i];

                           if (genParam.BaseType == parentType)
                           {
                              rewrittenGenParams[i] = tb;
                           }
                           else
                           {
                              rewrittenGenParams[i] = genParam;
                           }
                        }

                        return parameter.GetGenericTypeDefinition().MakeGenericType(rewrittenGenParams.ToArray());
                     }
                     else
                     {
                        return parameter;
                     }
                  }
               }
            }
            else
            {
               tb = moduleBuilder.DefineType(name, TypeAttributes.AutoClass | TypeAttributes.AutoLayout, parentType);
            }

            foreach (var customAttribExpression in attributesExpressions)
            {
               var body = (NewExpression)customAttribExpression.Body;

               tb.SetCustomAttribute(new CustomAttributeBuilder(body.Constructor!, body.Arguments.Cast<ConstantExpression>().Select(x => x.Value).ToArray()));
            }

#if NETSTANDARD2_0
            var newtype = tb.CreateTypeInfo()!.AsType()!;
#else
                var newtype = tb.CreateType()!;
#endif
            return newtype;
         });
      }
   }
}
