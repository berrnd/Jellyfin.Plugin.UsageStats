using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using System;

namespace Jellyfin.Plugin.InfluxDB
{
    public class Plugin : BasePlugin<PluginConfiguration>
    {
        public Plugin(IApplicationPaths appPaths, IXmlSerializer xmlSerializer) : base(appPaths, xmlSerializer)
        {
            Instance = this;
        }

        public override string Name => "InfluxDB";

        public static Plugin Instance { get; private set; }

        public override string Description => "Report (user) statistics to InfluxDB";

        public PluginConfiguration PluginConfiguration => Configuration;

        private readonly Guid _id = new Guid("a498e313-79cb-4275-a678-cfdf015f5349");
        public override Guid Id => _id;
    }
}
