namespace Acxiom.Domain.Common;

/// <summary>A business rule was broken. <see cref="Field"/> lets the UI show the message next to an input.</summary>
public class DomainException : Exception
{
    public string? Field { get; }

    public DomainException(string message, string? field = null) : base(message)
    {
        Field = field;
    }
}
