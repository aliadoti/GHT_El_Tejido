namespace ElTejido.Domain.Common;

public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

