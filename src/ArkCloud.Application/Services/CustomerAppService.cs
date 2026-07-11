using ArkCloud.Application.DTOs;
using ArkCloud.Application.Exceptions;
using ArkCloud.Application.Interfaces;
using ArkCloud.Domain.Entities;
using ArkCloud.Domain.ValueObjects;

namespace ArkCloud.Application.Services;

public class CustomerAppService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CustomerAppService(ICustomerRepository customerRepository, IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository;
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

        return new CustomerResponse
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

    public async Task<CustomerResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken)
                       ?? throw new NotFoundException("Customer not found.");

        return new CustomerResponse
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

    public async Task<List<CustomerResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var customers = await _customerRepository.GetAllAsync(cancellationToken);

        return customers.Select(customer => new CustomerResponse
        {
            Id = customer.Id,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Email = customer.Email.Value,
            Street = customer.Address.Street,
            City = customer.Address.City,
            Country = customer.Address.Country
        }).ToList();
    }
}
