using MediaBrowser.Model.Plugins;
using System;

namespace Jellyfin.Plugin.InfluxDB
{
	public class PluginConfiguration : BasePluginConfiguration
	{
		public String InfluxDbWriteBaseUri { get; set; } = "http://<host>:8086/write?db=emby";
	}
}
