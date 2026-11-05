using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Plugins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PathFilter;

/// <summary>
/// Tags new and refreshed items as they arrive, and re-syncs everything when the config is saved.
/// A metadata refresh that wipes tags fires ItemUpdated, so the tag is put straight back.
/// </summary>
public sealed class PathFilterHostedService : IHostedService
{
    private readonly ILibraryManager _libraryManager;
    private readonly PathFilterService _service;
    private readonly ILogger<PathFilterHostedService> _logger;

    public PathFilterHostedService(
        ILibraryManager libraryManager,
        PathFilterService service,
        ILogger<PathFilterHostedService> logger)
    {
        _libraryManager = libraryManager;
        _service = service;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemChanged;
        _libraryManager.ItemUpdated += OnItemChanged;
        if (Plugin.Instance is not null)
        {
            Plugin.Instance.ConfigurationChanged += OnConfigurationChanged;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemChanged;
        _libraryManager.ItemUpdated -= OnItemChanged;
        if (Plugin.Instance is not null)
        {
            Plugin.Instance.ConfigurationChanged -= OnConfigurationChanged;
        }

        return Task.CompletedTask;
    }

    private void OnItemChanged(object? sender, ItemChangeEventArgs e)
    {
        var item = e.Item;
        _ = Task.Run(async () =>
        {
            try
            {
                // Our own update fires ItemUpdated again; SyncItemAsync is a no-op then, so no loop.
                await _service.SyncItemAsync(item, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Path Filter: failed to tag {Item}", item.Path);
            }
        });
    }

    private void OnConfigurationChanged(object? sender, BasePluginConfiguration e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _service.FullSyncAsync(null, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Path Filter: sync after config change failed");
            }
        });
    }
}
