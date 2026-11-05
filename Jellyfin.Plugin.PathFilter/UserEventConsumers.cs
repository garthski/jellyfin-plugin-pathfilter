using System;
using System.Threading.Tasks;
using Jellyfin.Data.Events.Users;
using MediaBrowser.Controller.Events;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PathFilter;

/// <summary>New users get folder defaults applied the moment the account exists.</summary>
public class UserCreatedConsumer : IEventConsumer<UserCreatedEventArgs>
{
    private readonly PathFilterService _service;
    private readonly ILogger<UserCreatedConsumer> _logger;

    public UserCreatedConsumer(PathFilterService service, ILogger<UserCreatedConsumer> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task OnEvent(UserCreatedEventArgs eventArgs)
    {
        var userId = eventArgs.Argument.Id;
        await _service.SyncUserAsync(userId).ConfigureAwait(false);

        // The dashboard's "new user" form saves its own policy straight after creating the
        // account, which can overwrite the blocked tags we just set. Re-apply shortly after.
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                await _service.SyncUserAsync(userId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Path Filter: delayed sync for new user failed");
            }
        });
    }
}

/// <summary>
/// If someone edits a user's policy (e.g. parental controls page) and drops our tags,
/// put them back. Our own policy write is a no-op the second time round, so no loop.
/// </summary>
public class UserUpdatedConsumer : IEventConsumer<UserUpdatedEventArgs>
{
    private readonly PathFilterService _service;

    public UserUpdatedConsumer(PathFilterService service)
    {
        _service = service;
    }

    public Task OnEvent(UserUpdatedEventArgs eventArgs) => _service.SyncUserAsync(eventArgs.Argument.Id);
}
