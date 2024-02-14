using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.Core.Implementation;

internal static class CoreCompatExtensions
{
    public static IConfigurationArgumentValue GetArgumentValue(this IConfigurationSection argumentSection, ResolutionContext resolutionContext)
    {
        IConfigurationArgumentValue argumentValue;

        if (argumentSection.Value != null)
        {
            argumentValue = new StringArgumentValue(argumentSection, argumentSection.Value, argumentSection.Key);
        }
        else
        {
            argumentValue = new ObjectArgumentValue(argumentSection);
        }

        return argumentValue;
    }

    internal static Dictionary<string, (IConfigurationArgumentValue Value, IConfigurationSection Section)> Blank(this IConfigurationSection section)
        => new()
        {
            { string.Empty, (BlankConfigurationArgValue.Instance, section) },
        };

    internal static object Get(this IConfiguration configuration, Type type)
    {
        if (configuration is IConfigurationSection sec)
        {
            if (type.FullName == typeof(string).FullName)
            {
                return sec.Value!;
            }
            else if (type.FullName == typeof(int).FullName)
            {
                return int.Parse(sec.Value);
            }
            else
            {
                return Convert.ChangeType(sec.Value, type);
            }
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    internal static Delegate GenerateLambda(
       this ResolutionContext resolutionContext,
       MethodInfo configurationMethod,
       IConfigurationSection? sourceConfigurationSection,
       Type argumentType,
       string? originalKey)
        => throw new NotImplementedException();

    internal static void BindMappableValues(
        this ResolutionContext resolutionContext,
        object target,
        Type targetType,
        MethodInfo configurationMethod,
        IConfigurationSection sourceConfigurationSection,
        params string[] excludedKeys)
        => throw new NotImplementedException();

    private sealed class BlankConfigurationArgValue : IConfigurationArgumentValue
    {
        public static readonly IConfigurationArgumentValue Instance = new BlankConfigurationArgValue();

        public object? ConvertTo(MethodInfo configurationMethod, Type toType, ResolutionContext resolutionContext, string? providedKey = null)
        {
            return null;
        }
    }
}
