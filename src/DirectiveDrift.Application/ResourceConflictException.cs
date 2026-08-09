namespace DirectiveDrift.Application;

public sealed class ResourceConflictException(string message, Exception innerException)
    : Exception(message, innerException);
