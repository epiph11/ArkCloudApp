using ArkCloud.Domain.Exceptions;

namespace ArkCloud.Domain.ValueObjects;

public sealed class Email
{
    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email is required.");

        if (!value.Contains('@'))
            throw new DomainException("Email format is invalid.");

        return new Email(value.Trim().ToLowerInvariant());
    }

    public override string ToString() => Value;
}
