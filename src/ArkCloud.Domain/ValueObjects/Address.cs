using ArkCloud.Domain.Exceptions;

namespace ArkCloud.Domain.ValueObjects;

public sealed class Address
{
    public string Street { get; }
    public string City { get; }
    public string Country { get; }

    private Address(string street, string city, string country)
    {
        Street = street;
        City = city;
        Country = country;
    }

    public static Address Create(string street, string city, string country)
    {
        if (string.IsNullOrWhiteSpace(street))
            throw new DomainException("Street is required.");
        if (string.IsNullOrWhiteSpace(city))
            throw new DomainException("City is required.");
        if (string.IsNullOrWhiteSpace(country))
            throw new DomainException("Country is required.");

        return new Address(street.Trim(), city.Trim(), country.Trim());
    }
}
