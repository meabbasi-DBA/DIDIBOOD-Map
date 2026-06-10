using Didibood.LocationAccess.Application.LocationAccess;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Didibood.LocationAccess.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<LocationAccessRequestValidator>();
        return services;
    }
}
