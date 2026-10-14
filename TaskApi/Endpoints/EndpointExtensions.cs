using System.Reflection;

namespace TaskApi.Endpoints;

public static class EndpointExtensions
{
    public static IServiceCollection AddEndpointGroups(this IServiceCollection services)
    {
        var groupTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(IEndpointGroup).IsAssignableFrom(t)
                        && t is { IsAbstract: false, IsInterface: false });

        foreach (var type in groupTypes)
        {
            services.AddSingleton(typeof(IEndpointGroup), type);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        foreach (var group in app.ServiceProvider.GetServices<IEndpointGroup>())
        {
            group.Map(app);
        }

        return app;
    }
}