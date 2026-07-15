using ArkCloud.Domain.Common;
using ArkCloud.Domain.Exceptions;
using ArkCloud.Domain.ValueObjects;

namespace ArkCloud.Domain.Entities;

public class Customer : BaseEntity
{
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public Email Email { get; private set; }
    public Address Address { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Customer()
    {
        FirstName = default!;
        LastName = default!;
        Email = default!;
        Address = default!;
    }

    private Customer(string firstName, string lastName, Email email, Address address)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("First name is required.");
        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Last name is required.");

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email;
        Address = address;
        CreatedAt = DateTime.UtcNow;
    }

    public static Customer Create(string firstName, string lastName, Email email, Address address)
        => new(firstName, lastName, email, address);

    public void Update(string firstName, string lastName, Email email, Address address)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("First name is required.");
        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Last name is required.");

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email;
        Address = address;
    }
}
