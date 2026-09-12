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

    /// <summary>
    /// GDPR erasure when the row itself cannot be deleted: this customer has at least one
    /// order, and those orders are retained under the legal-obligation exception (RGPD art.
    /// 17(3)(b) — accounting/tax retention), which means orders.customer_id must keep pointing
    /// at a real row. Strips every personal field instead, using a deterministic-but-unique
    /// placeholder email per instance so the Email/uniqueness invariants stay satisfied
    /// (Email.Create still requires a value containing '@'). Idempotent: anonymizing an
    /// already-anonymized customer is a harmless no-op, not an error.
    /// See docs/rgpd-classification-donnees.md §3.
    /// </summary>
    public void Anonymize()
    {
        FirstName = "Anonymized";
        LastName = "Anonymized";
        Email = Email.Create($"anonymized+{Id:N}@arkcloud.invalid");
        Address = Address.Create("Anonymized", "Anonymized", "Anonymized");
    }
}
