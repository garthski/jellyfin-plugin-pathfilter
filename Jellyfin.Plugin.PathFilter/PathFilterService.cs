using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.PathFilter.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PathFilter;

/// <summary>
/// Keeps item tags and user blocked-tag lists in line with the plugin configuration.
/// Jellyfin itself does the hiding: every query, search, Next Up, Latest and direct
/// item lookup already honours a user's BlockedTags.
/// </summary>
public sealed class PathFilterService : IDisposable
{
    /// <summary>Every tag this plugin owns starts with this. Nothing else is ever touched.</summary>
    public const string TagPrefix = "pathfilter-";

    private static readonly BaseItemKind[] ManagedKinds =
    {
        BaseItemKind.Series,
        BaseItemKind.Season,
        BaseItemKind.Episode,
        BaseItemKind.Movie
    };

    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<PathFilterService> _logger;
    private readonly SemaphoreSlim _fullSyncLock = new(1, 1);

    public PathFilterService(
        ILibraryManager libraryManager,
        IUserManager userManager,
        ILogger<PathFilterService> logger)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _logger = logger;
    }

    private static PluginConfiguration Config => Plugin.Instance?.Configuration ?? new PluginConfiguration();

    public static string TagFor(Guid ruleId) => TagPrefix + ruleId.ToString("N").Substring(0, 8);

    private static bool IsOurTag(string tag) => tag.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Trim().Replace('\\', '/').TrimEnd('/');
    }

    private static bool IsUnder(string itemPath, string rulePath)
    {
        if (itemPath.Length == 0 || rulePath.Length == 0)
        {
            return false;
        }

        // Paths are case-sensitive on Linux; boundary check stops /shows2 matching /shows20.
        return itemPath.Equals(rulePath, StringComparison.Ordinal)
            || itemPath.StartsWith(rulePath + "/", StringComparison.Ordinal);
    }

    private static bool IsManaged(BaseItem item) => item is Series || item is Season || item is Episode || item is Movie;

    /// <summary>Virtual seasons/missing episodes have no path of their own, so fall back to the show folder.</summary>
    private static string ResolvePath(BaseItem item)
    {
        if (!string.IsNullOrEmpty(item.Path))
        {
            return Normalize(item.Path);
        }

        return item switch
        {
            Season season => Normalize(season.Series?.Path),
            Episode episode => Normalize(episode.Series?.Path),
            _ => string.Empty
        };
    }

    private static bool IsHiddenFor(PluginConfiguration cfg, FolderRule rule, Guid userId)
    {
        var ov = cfg.Overrides.FirstOrDefault(o => o.UserId == userId && o.RuleId == rule.Id);
        return ov is null ? rule.HiddenByDefault : !ov.Visible;
    }

    // ---------------------------------------------------------------- items

    /// <summary>Applies the right tags to one item. Returns true if it had to change anything.</summary>
    public async Task<bool> SyncItemAsync(BaseItem item, CancellationToken cancellationToken)
    {
        if (!IsManaged(item))
        {
            return false;
        }

        var cfg = Config;
        var path = ResolvePath(item);
        var current = item.Tags ?? Array.Empty<string>();

        var desired = current
            .Where(t => !IsOurTag(t))
            .Concat(cfg.Rules
                .Where(r => r.Id != Guid.Empty && IsUnder(path, Normalize(r.Path)))
                .Select(r => TagFor(r.Id)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (new HashSet<string>(current, StringComparer.OrdinalIgnoreCase).SetEquals(desired))
        {
            return false;
        }

        item.Tags = desired;
        await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
        return true;
    }

    // ---------------------------------------------------------------- users

    /// <summary>Rewrites only this plugin's entries in the user's BlockedTags; other blocked tags are kept.</summary>
    public async Task SyncUserAsync(Guid userId)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return;
        }

        var cfg = Config;
        var policy = _userManager.GetUserDto(user, null).Policy;
        if (policy is null)
        {
            return;
        }

        var exempt = cfg.ExemptAdministrators && policy.IsAdministrator;
        var hiddenTags = exempt
            ? Enumerable.Empty<string>()
            : cfg.Rules.Where(r => r.Id != Guid.Empty && IsHiddenFor(cfg, r, userId)).Select(r => TagFor(r.Id));

        var current = policy.BlockedTags ?? Array.Empty<string>();
        var desired = current
            .Where(t => !IsOurTag(t))
            .Concat(hiddenTags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (new HashSet<string>(current, StringComparer.OrdinalIgnoreCase).SetEquals(desired))
        {
            return;
        }

        policy.BlockedTags = desired;
        await _userManager.UpdatePolicyAsync(userId, policy).ConfigureAwait(false);
        _logger.LogInformation("Path Filter: updated blocked folders for user {User}", user.Username);
    }

    // ---------------------------------------------------------------- full sync

    /// <summary>Re-tags every TV/movie item and re-syncs every user. Safe to run repeatedly.</summary>
    public async Task FullSyncAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        await _fullSyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = ManagedKinds,
                Recursive = true
            });

            var changed = 0;
            for (var i = 0; i < items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (await SyncItemAsync(items[i], cancellationToken).ConfigureAwait(false))
                    {
                        changed++;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Path Filter: could not tag {Item}", items[i].Path);
                }

                progress?.Report(90.0 * (i + 1) / Math.Max(items.Count, 1));
            }

            foreach (var userId in _userManager.GetUsers().Select(u => u.Id).ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SyncUserAsync(userId).ConfigureAwait(false);
            }

            progress?.Report(100);
            _logger.LogInformation("Path Filter: full sync done, {Changed} of {Total} items re-tagged", changed, items.Count);
        }
        finally
        {
            _fullSyncLock.Release();
        }
    }

    public void Dispose() => _fullSyncLock.Dispose();
}
