using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PathFilter;

/// <summary>
/// Tags new and refreshed items as they arrive, re-syncs everything when the config is saved,
/// and runs a full sync after every library scan as a second net.
/// A metadata refresh that wipes tags fires ItemUpdated, so the tag is put straight back.
/// </summary>
public sealed class PathFilterHostedService : IHostedService
{
    /// <summary>Key of Jellyfin's built-in "Scan Media Library" scheduled task.</summary>
    private const string LibraryScanTaskKey = "RefreshLibrary";

    private readonly ILibraryManager _libraryManager;
    private readonly ITaskManager _taskManager;
    private readonly PathFilterService _service;
    private readonly ILogger<PathFilterHostedService> _logger;

    public PathFilterHostedService(
        ILibraryManager libraryManager,
        ITaskManager taskManager,
        PathFilterService service,
        ILogger<PathFilterHostedService> logger)
    {
        _libraryManager = libraryManager;
        _taskManager = taskManager;
        _service = service;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemChanged;
        _libraryManager.ItemUpdated += OnItemChanged;
        _taskManager.TaskCompleted += OnTaskCompleted;
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
        _taskManager.TaskCompleted -= OnTaskCompleted;
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
                await _service.SyncItemAsync(item, CancellationToken.None, logChange: true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Path Filter: failed to tag {Item}", item.Path);
            }
        });
    }

    private void OnTaskCompleted(object? sender, TaskCompletionEventArgs e)
    {
        if (!string.Equals(e.Task?.ScheduledTask?.Key, LibraryScanTaskKey, StringComparison.Ordinal))
        {
            return;
        }

        _ = RunFullSync("library scan finished");
    }

    private void OnConfigurationChanged(object? sender, BasePluginConfiguration e)
    {
        _ = RunFullSync("settings saved");
    }

    private Task RunFullSync(string reason) => Task.Run(async () =>
    {
        try
        {
            _logger.LogInformation("Path Filter: full sync ({Reason})", reason);
            await _service.FullSyncAsync(null, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Path Filter: full sync after {Reason} failed", reason);
        }
    });
}
