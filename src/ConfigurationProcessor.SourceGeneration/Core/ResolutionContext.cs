﻿using System.Reflection;
using ConfigurationProcessor.SourceGeneration.Utility;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.Core.Implementation;

internal delegate bool MethodFilter(MethodInfo methodInfo, string name);

internal delegate Type TypeResolver(MethodInfo? method, int index);

internal record class ResolutionContext(EmitContext EmitContext, IConfiguration RootConfiguration)
{
    public IConfiguration AppConfiguration => RootConfiguration;

    public List<Assembly> ConfigurationAssemblies => EmitContext.References;

    public TypeResolver CreateTypeResolver(string typeName, IConfiguration rootConfiguration, IConfiguration ambientConfiguration)
    {
        if (typeName[0] == '!' || typeName[0] == '@')
        {
            var newTypeName = typeName.Substring(1).ToString();
            return (method, argIndex) =>
            {
                if (newTypeName.IndexOf('@') >= 0)
                {
                    var split = newTypeName.Split('@');
                    return EmitContext.CreateType(split[0], CreateTypeResolver(split[1], rootConfiguration, ambientConfiguration)(method, argIndex));
                }
                else
                {
                    if (method == null)
                    {
                        throw new InvalidOperationException("Method cannot be null");
                    }

                    return EmitContext.CreateType(newTypeName);
                }
            };
        }
        else
        {
            return (method, _) => GetTypeInternal(typeName);
        }
    }

    public Assembly FindAssembly(string assemblyName)
    {
        Assembly? find = (from asm in ConfigurationAssemblies
                          where asm.FullName == assemblyName
                          select asm).SingleOrDefault();

        if (find == null)
        {
            find = Assembly.ReflectionOnlyLoad(assemblyName);
        }

        return find;
    }

    private Type GetTypeInternal(string typeName)
    {
        if (EmitContext.TypeMap.TryGetValue(typeName, out var values))
        {
            return values.Single();
        }
        else
        {
            throw new InvalidOperationException($"Cannot find type {typeName}");
        }
    }
}