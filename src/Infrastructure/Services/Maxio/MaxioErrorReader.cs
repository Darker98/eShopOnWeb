using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.eShopWeb.Infrastructure.Services.Maxio;

/// <summary>
/// Maxio declares three distinct error-response shapes across the operations this client calls
/// (Error-List-Response: {"errors": [...]}, Customer-Error-Response: {"errors": {"customer": "..."}}
/// or {"errors": [...]}, Single-Error-Response / Cancel-Subscription-Error-Response: {"error": "..."}).
/// This reads only the members those declared schemas actually have - never an invented field - and
/// falls back to the raw body for any shape the spec doesn't declare (fail-safe, not silently coerced).
/// </summary>
internal static class MaxioErrorReader
{
    public static IReadOnlyList<string> ExtractMessages(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return Array.Empty<string>();
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("errors", out var errors))
                {
                    return ReadStrings(errors);
                }

                if (root.TryGetProperty("error", out var singleError) && singleError.ValueKind == JsonValueKind.String)
                {
                    return new[] { singleError.GetString() ?? responseBody };
                }
            }
        }
        catch (JsonException)
        {
            // Not a JSON body we recognise - fall through and surface the raw text below.
        }

        return new[] { responseBody };
    }

    private static IReadOnlyList<string> ReadStrings(JsonElement element)
    {
        var messages = new List<string>();

        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        messages.Add(item.GetString()!);
                    }
                }
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        messages.Add($"{property.Name}: {property.Value.GetString()}");
                    }
                }
                break;
            case JsonValueKind.String:
                messages.Add(element.GetString()!);
                break;
        }

        return messages;
    }
}
