using MediaBrowser.Model.Plugins;
using System;

namespace Jellyfin.Plugin.UsageStats
{
	public class PluginConfiguration : BasePluginConfiguration
	{
		public String DBConnectionString { get; set; } = "Server=<Host>;Database=<Database>;User ID=<Username>;Password=<Password>";
	}
}
