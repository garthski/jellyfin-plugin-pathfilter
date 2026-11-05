using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.PathFilter;

/// <summary>
/// Safety net: re-checks every item and user at startup and every 12 hours.
/// Shows up under Dashboard > Scheduled Tasks > Path Filter, where it can be run by hand.
/// </summary>
public class PathFilterSyncTask : IScheduledTask
{
    private readonly PathFilterService _service;

    public PathFilterSyncTask(PathFilterService service)
    {
        _service = service;
    }

    public string Name => "Sync folder restrictions";

    public string Key => "PathFilterFullSync";

    public string Description => "Re-applies Path Filter tags to every show and movie and re-syncs every user's hidden folders.";

    public string Category => "Path Filter";

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => _service.FullSyncAsync(progress, cancellationToken);

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo { Type = TaskTriggerInfoType.StartupTrigger };
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(12).Ticks
        };
    }
}
