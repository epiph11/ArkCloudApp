using ArkCloud.Application.DTOs;
using ArkCloud.Application.Exceptions;
using ArkCloud.Application.Interfaces;
using ArkCloud.Domain.Entities;
using ArkCloud.Domain.ValueObjects;

namespace ArkCloud.Application.Services;

public class CustomerAppService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CustomerAppService(
        ICustomerRepository customerRepository,
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var customer = Customer.Create(
            request.FirstName,
            request.LastName,
            Email.Create(request.Email),
            Address.Create(request.Street, request.City, request.Country));

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(customer);
    }

    public async Task<CustomerResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken)
                       ?? throw new NotFoundException("Customer not found.");

        return ToResponse(customer);
    }

    public async Task<List<CustomerResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var customers = await _customerRepository.GetAllAsync(cancellationToken);

        return customers.Select(ToResponse).ToList();
    }

    public async Task<PagedResult<CustomerResponse>> GetPagedAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var (items, totalCount) = await _customerRepository.GetPagedAsync(search, page, pageSize, cancellationToken);

        return new PagedResult<CustomerResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CustomerResponse> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken)
                       ?? throw new NotFoundException("Customer not found.");

        customer.Update(
            request.FirstName,
            request.LastName,
            Email.Create(request.Email),
            Address.Create(request.Street, request.City, request.Country));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(customer);
    }

    /// <summary>
    /// GDPR erasure (RGPD art. 17). Two paths, chosen by whether this customer has orders —
    /// see docs/rgpd-classification-donnees.md §3 for the finding this fixes (orders.customer_id
    /// used to go orphan on delete, because no decision had been made between hard-deleting,
    /// anonymizing, or documenting retention):
    ///   - No orders: nothing needs to be kept for accounting purposes, so this is a genuine
    ///     hard delete, same as before.
    ///   - At least one order: those orders are retained under the legal-obligation exception
    ///     (art. 17(3)(b) — accounting/tax retention), so the Customer row must keep existing
    ///     for orders.customer_id to remain valid. The row is anonymized in place instead of
    ///     deleted — this still satisfies the erasure request for every field that's actually
    ///     personal data.
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken)
                       ?? throw new NotFoundException("Customer not found.");

        var hasOrders = await _orderRepository.ExistsForCustomerAsync(id, cancellationToken);

        if (hasOrders)
        {
            customer.Anonymize();
        }
        else
        {
            _customerRepository.Remove(customer);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>internal, not private: reused by DashboardAppService's "latest customers" widget.</summary>
    internal static CustomerResponse ToResponse(Customer customer) => new()
    {
        Id = customer.Id,
        FirstName = customer.FirstName,
        LastName = customer.LastName,
        Email = customer.Email.Value,
        Street = customer.Address.Street,
        City = customer.Address.City,
        Country = customer.Address.Country
    };
}
