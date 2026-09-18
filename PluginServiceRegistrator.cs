using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ArtistTagShelf;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton(sp =>
            new HttpCache(sp.GetRequiredService<IApplicationPaths>(), "artisttagshelf", "artistfin"));
        serviceCollection.AddSingleton<ArtistLookupClient>();
        serviceCollection.AddSingleton<ArtistEngine>();
        serviceCollection.AddSingleton<IRemoteMetadataProvider<MusicArtist, ArtistInfo>, ArtistTagShelfMetadataProvider>();
        serviceCollection.AddSingleton<IRemoteImageProvider, ArtistTagShelfImageProvider>();
    }
}
