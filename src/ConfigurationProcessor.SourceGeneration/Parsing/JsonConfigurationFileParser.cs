using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.SourceGeneration.Parsing;

/// <summary>
/// This implementation is copied from Microsoft.Extensions.Configuration.Json
/// </summary>
public sealed class JsonConfigurationFileParser
{
    private readonly Dictionary<string, string?> data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<string> paths = new Stack<string>();

    private JsonConfigurationFileParser()
    {
    }

    /// <summary>
    /// Parses a json input stream into key value pairs.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static IDictionary<string, string?> Parse(Stream input)
        => new JsonConfigurationFileParser().ParseStream(input);

    private IDictionary<string, string?> ParseStream(Stream input)
    {
        var jsonDocumentOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        using (var reader = new StreamReader(input))
        using (JsonDocument doc = JsonDocument.Parse(reader.ReadToEnd(), jsonDocumentOptions))
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException(string.Format("Invalid top level json element {0}", doc.RootElement.ValueKind));
            }

            VisitObjectElement(doc.RootElement);
        }

        return data;
    }

    private void VisitObjectElement(JsonElement element)
    {
        var isEmpty = true;

        foreach (JsonProperty property in element.EnumerateObject())
        {
            isEmpty = false;
            EnterContext(property.Name);
            VisitValue(property.Value);
            ExitContext();
        }

        SetNullIfElementIsEmpty(isEmpty);
    }

    private void VisitArrayElement(JsonElement element)
    {
        int index = 0;

        foreach (JsonElement arrayElement in element.EnumerateArray())
        {
            EnterContext(index.ToString());
            VisitValue(arrayElement);
            ExitContext();
            index++;
        }

        SetNullIfElementIsEmpty(isEmpty: index == 0);
    }

    private void SetNullIfElementIsEmpty(bool isEmpty)
    {
        if (isEmpty && paths.Count > 0)
        {
            data[paths.Peek()] = null;
        }
    }

    private void VisitValue(JsonElement value)
    {
#pragma warning disable SA1405 // Debug.Assert should provide message text
        Debug.Assert(paths.Count > 0);
#pragma warning restore SA1405 // Debug.Assert should provide message text

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                VisitObjectElement(value);
                break;

            case JsonValueKind.Array:
                VisitArrayElement(value);
                break;

            case JsonValueKind.Number:
            case JsonValueKind.String:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                string key = paths.Peek();
                if (data.ContainsKey(key))
                {
                    throw new FormatException(string.Format("Key {0} is duplicated", key));
                }

                data[key] = value.ToString();
                break;

            default:
                throw new FormatException(string.Format("Unsupported json token {0}", value.ValueKind));
        }
    }

    private void EnterContext(string context) =>
        paths.Push(paths.Count > 0 ?
            paths.Peek() + ConfigurationPath.KeyDelimiter + context :
            context);

    private void ExitContext() => paths.Pop();
}
