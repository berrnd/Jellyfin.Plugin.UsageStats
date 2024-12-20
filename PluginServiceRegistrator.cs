using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.UsageStats
{
	public class PluginServiceRegistrator : IPluginServiceRegistrator
	{
		public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
		{
			serviceCollection.AddHostedService<JellyfinUsageStats>();
		}
	}
}
