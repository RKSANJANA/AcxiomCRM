namespace Acxiom.Application.Common;

public class NotFoundException : Exception
{
    public NotFoundException(string what) : base($"{what} was not found.") { }
}

public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "You don't have permission to do that.") : base(message) { }
}

/// <summary>The request clashes with existing data (duplicate email etc.).</summary>
public class ConflictException : Exception
{
    public string? Field { get; }

    public ConflictException(string message, string? field = null) : base(message)
    {
        Field = field;
    }
}
