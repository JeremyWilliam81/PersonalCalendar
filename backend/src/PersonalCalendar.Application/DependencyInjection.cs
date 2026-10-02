using Microsoft.Extensions.DependencyInjection;
using PersonalCalendar.Application.Events;
using PersonalCalendar.Application.Time;

namespace PersonalCalendar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ZoneLookup>();
        services.AddScoped<CreateEvent>();
        services.AddScoped<GetMonthView>();
        services.AddScoped<GetDaysView>();
        services.AddScoped<GetEventDetails>();
        services.AddScoped<UpdateEvent>();
        services.AddScoped<DeleteEvent>();
        return services;
    }
}
