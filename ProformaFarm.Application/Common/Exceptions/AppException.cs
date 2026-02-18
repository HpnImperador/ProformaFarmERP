using System;
using System.Collections.Generic;
using System.Text;

namespace ProformaFarm.Application.Common.Exceptions;

public abstract class AppException : Exception
{
    public string Code { get; }
    protected AppException(string code, string message) : base(message) => Code = code;
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message = "Resource not found")
        : base("NOT_FOUND", message) { }
}

public sealed class ValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors, string message = "Validation failed")
        : base("VALIDATION_ERROR", message) => Errors = errors;
}

public sealed class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string message = "Unauthorized")
        : base("UNAUTHORIZED", message) { }
}

public sealed class ForbiddenAppException : AppException
{
    public ForbiddenAppException(string message = "Forbidden")
        : base("FORBIDDEN", message) { }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string message = "Conflict")
        : base("CONFLICT", message) { }
}

