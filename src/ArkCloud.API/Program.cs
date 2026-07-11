using ArkCloud.API.Middlewares;
using ArkCloud.Application.Services;
using ArkCloud.Application.Validators;
using ArkCloud.Infrastructure.DependencyInjection;
using FluentValidation;
using Serilog;
using Serilog.Formatting.Compact;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ArkCloud API",
        Version = "v1"
    });
});

// FluentValidation.AspNetCore (automatic MVC validation) is deprecated and was removed.
// Validators are registered here via FluentValidation.DependencyInjectionExtensions and
// invoked explicitly in each controller action with IValidator<T>.ValidateAndThrowAsync(),
// which throws FluentValidation.ValidationException — caught by ExceptionHandlingMiddleware
// and turned into a 400 application/problem+json response.
builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerRequestValidator>();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<CustomerAppService>();
builder.Services.AddScoped<OrderAppService>();
builder.Services.AddScoped<ProductAppService>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program { }
