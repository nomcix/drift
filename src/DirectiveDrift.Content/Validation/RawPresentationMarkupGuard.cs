using System.Globalization;
using System.Text.Json;

namespace DirectiveDrift.Content.Validation;

public static class RawPresentationMarkupGuard
{
    private static readonly string[] MarkupFragments =
    [
        "<svg",
        "</svg",
        "<style",
        "</style",
        "style=",
        "javascript:",
        "url(",
    ];

    public static ValidationReport Validate(string json)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return ValidationReport.Valid;
        }

        using (document)
        {
            var errors = new List<ValidationError>();
            Inspect(document.RootElement, "/", errors);

            return new ValidationReport(
                errors
                    .OrderBy(error => error.Path, StringComparer.Ordinal)
                    .ToArray());
        }
    }

    private static void Inspect(
        JsonElement element,
        string path,
        List<ValidationError> errors)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPath = Append(path, property.Name);

                    if (IsMarkupPropertyName(property.Name))
                    {
                        errors.Add(CreateError(propertyPath));
                    }

                    Inspect(property.Value, propertyPath, errors);
                }

                break;

            case JsonValueKind.Array:
                var index = 0;

                foreach (var item in element.EnumerateArray())
                {
                    Inspect(
                        item,
                        Append(path, index.ToString(CultureInfo.InvariantCulture)),
                        errors);
                    index++;
                }

                break;

            case JsonValueKind.String:
                var value = element.GetString() ?? string.Empty;

                if (ContainsMarkup(value) || LooksLikeCss(value))
                {
                    errors.Add(CreateError(path));
                }

                break;
        }
    }

    private static bool IsMarkupPropertyName(string name)
    {
        return name.Contains("svg", StringComparison.OrdinalIgnoreCase)
            || name.Contains("css", StringComparison.OrdinalIgnoreCase)
            || name.Equals("style", StringComparison.OrdinalIgnoreCase)
            || name.Equals("markup", StringComparison.OrdinalIgnoreCase)
            || name.Equals("html", StringComparison.OrdinalIgnoreCase)
            || name.Equals("pathData", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsMarkup(string value)
    {
        return MarkupFragments.Any(fragment =>
            value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeCss(string value)
    {
        return value.Contains('{')
            && value.Contains('}')
            && value.Contains(':')
            && value.Contains(';');
    }

    private static ValidationError CreateError(string path)
    {
        return new ValidationError(
            ValidationErrorCodes.ContentRawPresentationMarkup,
            path,
            "Raw SVG, CSS, or HTML presentation content is not accepted.");
    }

    private static string Append(string path, string segment)
    {
        var escaped = segment.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);

        return path == "/" ? $"/{escaped}" : $"{path}/{escaped}";
    }
}
