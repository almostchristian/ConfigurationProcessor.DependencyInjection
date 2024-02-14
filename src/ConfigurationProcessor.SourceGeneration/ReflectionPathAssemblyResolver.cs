// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace ConfigurationProcessor;

/// <summary>
/// Combines <see cref="PathAssemblyResolver"/> with a dynamic type builder.
/// </summary>
public sealed class ReflectionPathAssemblyResolver : PathAssemblyResolver
{
    internal static readonly AssemblyName DynamicAssemblyName = new AssemblyName(Guid.NewGuid().ToString());
    private static readonly AssemblyBuilder AssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(DynamicAssemblyName, AssemblyBuilderAccess.RunAndCollect);
    private static readonly ModuleBuilder ModuleBuilder = AssemblyBuilder.DefineDynamicModule("DynamicClass");
    private readonly ConcurrentDictionary<(string Name, Type? ParentType), Lazy<Type>> dynamicTypes = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ReflectionPathAssemblyResolver"/> class.
    /// </summary>
    /// <param name="assemblyPaths"></param>
    public ReflectionPathAssemblyResolver(IEnumerable<string> assemblyPaths)
        : base(assemblyPaths)
    {
    }

    /// <inheritdoc/>
    public override Assembly? Resolve(MetadataLoadContext context, AssemblyName assemblyName)
    {
        if (assemblyName.Name == DynamicAssemblyName.Name)
        {
            return AssemblyBuilder;
        }

        return base.Resolve(context, assemblyName);
    }

    internal Type CreateType(string name, Type? parentType)
    {
        return dynamicTypes.GetOrAdd((name, parentType), n => new Lazy<Type>(() => CreateDynamicType(n.Name, n.ParentType))).Value;

        Type CreateDynamicType(string name, Type? parentType)
        {
            var originalParentType = parentType;

            TypeBuilder tb;
            bool isParentTypeOpenGeneric = false;

            string typeNameWithNamespace = name;

            if (parentType != null)
            {
                if (parentType.IsGenericTypeDefinition)
                {
                    isParentTypeOpenGeneric = true;
                    tb = ModuleBuilder.DefineType(typeNameWithNamespace, TypeAttributes.AutoClass | TypeAttributes.BeforeFieldInit);
                    var derivedParentType = parentType.MakeGenericType(tb);

                    parentType = derivedParentType;
                    tb.SetParent(derivedParentType);
                }
                else
                {
                    tb = ModuleBuilder.DefineType(typeNameWithNamespace, TypeAttributes.AutoClass | TypeAttributes.AutoLayout);
                    tb.SetParent(parentType);
                }
            }
            else
            {
                tb = ModuleBuilder.DefineType(typeNameWithNamespace, TypeAttributes.AutoClass | TypeAttributes.AutoLayout);
            }

            if (parentType != null && originalParentType!.GetConstructor(Type.EmptyTypes) == null)
            {
                // assumes protected constructors
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
                var declaredConstructor = originalParentType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields

                foreach (var constructor in declaredConstructor)
                {
                    var parameters = constructor.GetParameters();
                    if (parameters.Length > 0 && parameters[^1].IsDefined(typeof(ParamArrayAttribute), false))
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

                    var derivedConstructor = isParentTypeOpenGeneric ? TypeBuilder.GetConstructor(parentType, constructor) : constructor;
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

                                if (genParam.BaseType == originalParentType)
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

            try
            {
                return tb.CreateTypeInfo()!.AsType()!;
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException($"Failed to create type {name}{(originalParentType != null ? $" with parent {originalParentType}" : string.Empty)}", ex);
            }
        }
    }
}