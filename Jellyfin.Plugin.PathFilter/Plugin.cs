using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.PathFilter.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.PathFilter;

/// <summary>
/// Path Filter: hide folders inside a library from specific users.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Path Filter";

    public override Guid Id => Guid.Parse("38f287a9-5faf-4457-8811-d088e3dbf5dc");

    public override string Description =>
        "Per-user folder visibility inside a library, enforced through Jellyfin's blocked tags.";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Configuration.configPage.html",
                    GetType().Namespace)
            }
        };
    }
}
