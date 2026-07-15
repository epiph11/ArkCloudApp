namespace ArkCloud.Domain.Exceptions;

public class InvalidOrderStateException : DomainException
{
    public InvalidOrderStateException(string message) : base(message)
    {
    }
}
