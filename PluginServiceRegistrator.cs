using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ArtistFin;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<HttpCache>();
        serviceCollection.AddSingleton<ArtistLookupClient>();
        serviceCollection.AddSingleton<ArtistEngine>();
        serviceCollection.AddSingleton<IRemoteMetadataProvider<MusicArtist, ArtistInfo>, ArtistFinMetadataProvider>();
        serviceCollection.AddSingleton<IRemoteImageProvider, ArtistFinImageProvider>();
    }
}
