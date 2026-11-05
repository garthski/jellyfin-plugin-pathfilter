using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.PathFilter.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Folders the plugin manages, each with a default visibility.</summary>
    public List<FolderRule> Rules { get; set; } = new();

    /// <summary>Explicit per-user settings. These always win over the folder default until removed.</summary>
    public List<UserOverride> Overrides { get; set; } = new();

    /// <summary>When true, administrators are never restricted.</summary>
    public bool ExemptAdministrators { get; set; } = true;
}

public class FolderRule
{
    /// <summary>Stable id; the item tag is derived from it, so renaming the path keeps the same tag.</summary>
    public Guid Id { get; set; }

    /// <summary>Absolute folder path as Jellyfin sees it, e.g. /media/tv/shows2.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Default for every user without an override (new users included).</summary>
    public bool HiddenByDefault { get; set; }
}

public class UserOverride
{
    public Guid UserId { get; set; }

    public Guid RuleId { get; set; }

    /// <summary>True = user can see the folder, false = hidden, regardless of the default.</summary>
    public bool Visible { get; set; }
}
