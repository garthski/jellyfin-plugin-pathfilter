using Jellyfin.Data.Events.Users;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.PathFilter;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<PathFilterService>();
        serviceCollection.AddHostedService<PathFilterHostedService>();
        serviceCollection.AddScoped<IEventConsumer<UserCreatedEventArgs>, UserCreatedConsumer>();
        serviceCollection.AddScoped<IEventConsumer<UserUpdatedEventArgs>, UserUpdatedConsumer>();
    }
}
