using System.Text.Json;
using Json.Schema;

namespace DirectiveDrift.Content.Validation;

public static class JsonSchemaContractValidator
{
    private static readonly EvaluationOptions EvaluationOptions = new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true,
    };

    public static ValidationReport Validate(string json, string schemaJson)
    {
        using var instance = ParseInstance(json, out var parseError);

        if (parseError is not null)
        {
            return new ValidationReport([parseError]);
        }

        JsonSchema schema;

        try
        {
            schema = JsonSchema.FromText(
                schemaJson,
                new BuildOptions
                {
                    SchemaRegistry = new SchemaRegistry(),
                });
        }
        catch (JsonException exception)
        {
            return new ValidationReport(
            [
                new ValidationError(
                    ValidationErrorCodes.SchemaDefinitionInvalid,
                    NormalizePath(exception.Path),
                    "The JSON Schema document is invalid."),
            ]);
        }

        var evaluation = schema.Evaluate(instance!.RootElement, EvaluationOptions);

        if (evaluation.IsValid)
        {
            return ValidationReport.Valid;
        }

        var errors = Flatten(evaluation)
            .Where(result => result.Errors is { Count: > 0 })
            .SelectMany(result => result.Errors!.Select(error =>
                new ValidationError(
                    MapSchemaErrorCode(error.Key),
                    NormalizePath(result.InstanceLocation.ToString()),
                    error.Value)))
            .Distinct()
            .OrderBy(error => error.Path, StringComparer.Ordinal)
            .ThenBy(error => error.Code, StringComparer.Ordinal)
            .ThenBy(error => error.Message, StringComparer.Ordinal)
            .ToArray();

        return errors.Length == 0
            ? new ValidationReport(
            [
                new ValidationError(
                    ValidationErrorCodes.SchemaViolation,
                    "/",
                    "The document does not satisfy its JSON Schema."),
            ])
            : new ValidationReport(errors);
    }

    private static JsonDocument? ParseInstance(string json, out ValidationError? error)
    {
        try
        {
            error = null;
            return JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 128,
                });
        }
        catch (JsonException exception)
        {
            error = new ValidationError(
                ValidationErrorCodes.JsonMalformed,
                NormalizePath(exception.Path),
                "The document is not valid JSON.");
            return null;
        }
    }

    private static IEnumerable<EvaluationResults> Flatten(EvaluationResults root)
    {
        yield return root;

        if (root.Details is null)
        {
            yield break;
        }

        foreach (var detail in root.Details)
        {
            foreach (var descendant in Flatten(detail))
            {
                yield return descendant;
            }
        }
    }

    private static string MapSchemaErrorCode(string keyword)
    {
        return string.Equals(keyword, "additionalProperties", StringComparison.Ordinal)
            ? ValidationErrorCodes.SchemaAdditionalProperties
            : ValidationErrorCodes.SchemaViolation;
    }

    private static string NormalizePath(string? path)
    {
        return string.IsNullOrEmpty(path) ? "/" : path;
    }
}
