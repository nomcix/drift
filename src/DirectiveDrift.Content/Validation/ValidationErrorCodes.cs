namespace DirectiveDrift.Content.Validation;

public static class ValidationErrorCodes
{
    public const string JsonMalformed = "json.malformed";
    public const string SchemaDefinitionInvalid = "schema.definition-invalid";
    public const string SchemaViolation = "schema.violation";
    public const string SchemaAdditionalProperties = "schema.additional-properties";
    public const string ContractDeserializationFailed = "contract.deserialization-failed";
    public const string ContentFileReadFailed = "content.file-read-failed";
    public const string ContentDuplicateId = "content.duplicate-id";
    public const string ContentUnresolvedReference = "content.unresolved-reference";
    public const string ContentInvalidReference = "content.invalid-reference";
    public const string ContentRawPresentationMarkup = "content.raw-presentation-markup";
}
